using System;
using System.Collections.Generic;
using System.Linq;
using Njsast.Ast;
using Njsast.Output;
using Njsast.Reader;

namespace Njsast.EsmToCjs;

public class EsmToCjsTreeTransformer : TreeTransformer
{
    const int ExportPreinitChunkSize = 50;
    readonly bool _includeExportSetters;
    uint _moduleVarCounter;
    uint _defaultExportCounter;
    readonly Dictionary<SymbolDef, Func<AstNode, AstNode>> _importReferenceReplacements = new();
    readonly Dictionary<string, Func<AstNode, AstNode>> _unresolvedImportReferenceReplacements = new();
    readonly Dictionary<SymbolDef, bool> _liveImportBindings = new();
    readonly Dictionary<string, uint> _moduleNameCounters = new();
    readonly Dictionary<string, List<string>> _deferredLocalExports = new();
    readonly Dictionary<string, List<string>> _exportedLocalAssignments = new();
    readonly HashSet<string> _consumedDeferredLocalExports = new();
    readonly List<string> _hoistedTempNames = new();
    readonly List<string> _exportPreinitNames = new();
    readonly List<AstNode> _hoistedExportAssignments = new();
    uint _tempVarCounter;

    public EsmToCjsTreeTransformer(bool includeExportSetters = false)
    {
        _includeExportSetters = includeExportSetters;
    }

    protected override AstNode? Before(AstNode node, bool inList)
    {
        if (node is AstToplevel toplevel)
            RegisterExportedLocalReferences(toplevel);

        if (node is AstSymbolRef symbolRef && symbolRef.Thedef != null &&
            _importReferenceReplacements.TryGetValue(symbolRef.Thedef, out var replacement))
            return replacement(symbolRef);
        if (node is AstSymbolRef unresolvedSymbolRef &&
            (unresolvedSymbolRef.Thedef == null || unresolvedSymbolRef.Thedef.Undeclared ||
             unresolvedSymbolRef.Thedef.Scope is AstToplevel) &&
            _unresolvedImportReferenceReplacements.TryGetValue(unresolvedSymbolRef.Name, out var unresolvedReplacement))
            return unresolvedReplacement(unresolvedSymbolRef);

        return node switch
        {
            AstImport import => TransformImport(import),
            AstExport export => TransformExport(export),
            AstAssign assignment => TransformExportedLocalAssignment(assignment),
            AstImportExpression importExpr => TransformDynamicImport(importExpr),
            _ => null
        };
    }

    protected override AstNode? After(AstNode node, bool inList)
    {
        if (node is AstToplevel toplevel)
        {
            toplevel.HasUseStrictDirective = true;
            FinalizeToplevel(toplevel);
        }
        return null;
    }

    void FinalizeToplevel(AstToplevel toplevel)
    {
        var prepend = new StructRefList<AstNode>();
        HoistDecoratedClassSelfReferenceAliasVars(toplevel, ref prepend);
        if (_hoistedTempNames.Count > 0)
            prepend.Add(MakeTempVarStatement(toplevel));

        if (_exportPreinitNames.Count > 0)
            AddExportsPreinitStatements(toplevel, ref prepend);

        foreach (var assignment in _hoistedExportAssignments)
            prepend.Add(assignment);

        PatchExportedEnumIifes(toplevel);
        InsertDeferredLocalExports(toplevel);

        if (prepend.Count == 0)
            return;

        for (var i = (int)prepend.Count - 1; i >= 0; i--)
            toplevel.Body.Insert(0, prepend[(uint)i]);
    }

    void HoistDecoratedClassSelfReferenceAliasVars(AstToplevel toplevel, ref StructRefList<AstNode> prepend)
    {
        for (var i = 0; i < toplevel.Body.Count;)
        {
            if (toplevel.Body[i] is AstVar { Definitions.Count: 1 } varStatement &&
                varStatement.Definitions[0] is { Value: null, Name: AstSymbolVar { Name: var name } } &&
                IsDecoratedClassSelfReferenceAliasName(name))
            {
                prepend.Add(varStatement);
                toplevel.Body.RemoveAt(i);
                continue;
            }

            i++;
        }
    }

    static bool IsDecoratedClassSelfReferenceAliasName(string name)
    {
        var underscore = name.LastIndexOf('_');
        if (underscore <= 0 || underscore == name.Length - 1)
            return false;
        for (var i = underscore + 1; i < name.Length; i++)
            if (!char.IsDigit(name[i]))
                return false;
        return true;
    }

    void RegisterExportedLocalReferences(AstToplevel toplevel)
    {
        foreach (var statement in toplevel.Body.AsReadOnlySpan())
        {
            switch (statement)
            {
                case AstExport { ModuleName: null, ExportedNames.Count: > 0 } export when _includeExportSetters:
                    foreach (var mapping in export.ExportedNames.AsReadOnlySpan())
                    {
                        if (IsErasedTypeOnlyLocalExport(mapping))
                            continue;
                        if (mapping.Name.Thedef is { Orig.Count: > 0 } def &&
                            def.Orig[0] is AstSymbolImport)
                            AddDeferredLocalExport(mapping.Name.Name, mapping.ForeignName.Name);
                        else
                            AddExportedLocalAssignment(mapping.Name.Name, mapping.ForeignName.Name);
                    }

                    break;
                case AstExport { ExportedDefinition: AstDefinitions definitions }:
                    foreach (var definition in definitions.Definitions.AsReadOnlySpan())
                    {
                        if (definition.Name is AstSymbol symbol && HasReferenceBeforeDeclaration(symbol))
                            RegisterExportReplacement(symbol);
                        else if (definition.Name is AstDestructuring destructuring)
                            RegisterExportedDestructuringReferences(destructuring);
                    }

                    break;
            }
        }
    }

    static bool HasReferenceBeforeDeclaration(AstSymbol symbol)
    {
        if (symbol.Thedef == null)
            return false;
        foreach (var reference in symbol.Thedef.References.AsReadOnlySpan())
        {
            if (reference.Start.Index < symbol.Start.Index)
                return true;
        }
        return false;
    }

    void AddDeferredLocalExport(string localName, string exportedName)
    {
        if (!_deferredLocalExports.TryGetValue(localName, out var exportedNames))
        {
            exportedNames = new List<string>();
            _deferredLocalExports.Add(localName, exportedNames);
        }

        exportedNames.Add(exportedName);
    }

    void AddExportedLocalAssignment(string localName, string exportedName)
    {
        if (!_exportedLocalAssignments.TryGetValue(localName, out var exportedNames))
        {
            exportedNames = new List<string>();
            _exportedLocalAssignments.Add(localName, exportedNames);
        }

        if (!exportedNames.Contains(exportedName))
            exportedNames.Add(exportedName);
    }

    void RegisterExportedDestructuringReferences(AstDestructuring destructuring)
    {
        var symbols = new List<AstSymbol>();
        CollectDestructuringSymbols(destructuring, symbols);
        foreach (var symbol in symbols)
        {
            AddExportedLocalAssignment(symbol.Name, symbol.Name);
            RegisterExportReplacement(symbol);
        }
    }

    static void CollectDestructuringSymbols(AstNode node, List<AstSymbol> symbols)
    {
        switch (node)
        {
            case AstSymbol symbol:
                symbols.Add(symbol);
                break;
            case AstObjectKeyVal keyValue:
                CollectDestructuringSymbols(keyValue.Value, symbols);
                break;
            case AstDefaultAssign defaultAssign:
                CollectDestructuringSymbols(defaultAssign.Left, symbols);
                break;
            case AstDestructuring destructuring:
                foreach (var name in destructuring.Names.AsReadOnlySpan())
                    CollectDestructuringSymbols(name, symbols);
                break;
            case AstExpansion expansion:
                CollectDestructuringSymbols(expansion.Expression, symbols);
                break;
        }
    }

    void PatchExportedEnumIifes(AstToplevel toplevel)
    {
        foreach (var statement in toplevel.Body.AsReadOnlySpan())
        {
            if (statement is not AstSimpleStatement
                {
                    Body: AstCall
                    {
                        Args.Count: 1,
                    } call
                })
                continue;

            if (call.Args[0] is not AstBinary
                {
                    Operator: Operator.LogicalOr,
                    Left: AstSymbolRef enumRef,
                    Right: AstAssign
                    {
                        Operator: Operator.Assignment,
                        Left: AstSymbolRef enumAssignRef
                    } enumAssign
                })
                continue;

            if (enumRef.Name != enumAssignRef.Name || !_exportPreinitNames.Contains(enumRef.Name))
                continue;

            call.Args.SetItem(0, new AstBinary(enumRef.Source, enumRef.Start, enumAssign.End,
                enumRef,
                MakeExportsAssign(enumAssign, enumRef.Name, enumAssign),
                Operator.LogicalOr));
        }
    }

    void InsertDeferredLocalExports(AstToplevel toplevel)
    {
        for (var i = 0; i < toplevel.Body.Count; i++)
        {
            var statements = new StructRefList<AstNode>();
            switch (toplevel.Body[i])
            {
                case AstDefinitions defs:
                    foreach (var def in defs.Definitions.AsReadOnlySpan())
                    {
                        if (def.Name is AstSymbol symbol)
                            AddDeferredExportStatements(symbol, ref statements);
                    }

                    break;
                case AstDefun { Name: not null } defun:
                    AddDeferredExportStatements(defun.Name, ref statements);
                    break;
                case AstDefClass { Name: not null } defClass:
                    AddDeferredExportStatements(defClass.Name, ref statements);
                    break;
            }

            for (var j = 0; j < statements.Count; j++)
                toplevel.Body.Insert((int)i + 1 + j, statements[j]);
            i += (int)statements.Count;
        }
    }

    void AddDeferredExportStatements(AstSymbol symbol, ref StructRefList<AstNode> statements)
    {
        if (!_deferredLocalExports.TryGetValue(symbol.Name, out var exportedNames))
            return;

        foreach (var exportedName in exportedNames)
        {
            var value = MakeImportedOrLocalRef(symbol, symbol);
            if (symbol.Thedef != null && _liveImportBindings.ContainsKey(symbol.Thedef))
                statements.Add(MakeSimpleStatement(symbol,
                    MakeDefineExportGetterCall(symbol, exportedName, value,
                        _includeExportSetters)));
            else
                statements.Add(MakeExportsAssignStatement(symbol, exportedName, value));
        }
        _deferredLocalExports.Remove(symbol.Name);
        _consumedDeferredLocalExports.Add(symbol.Name);
    }

    AstNode TransformImport(AstImport import)
    {
        var moduleExpr = import.ModuleName;
        var importName = HasRuntimeReferences(import.ImportedName) ? import.ImportedName : null;
        var importedNames = FilterRuntimeImportMappings(import.ImportedNames);

        // import "mod" — side effect only
        if (import.ImportedName == null && import.ImportedNames.Count == 0)
        {
            return MakeSimpleStatement(import, MakeRequireCall(import, moduleExpr));
        }

        if (importName == null && importedNames.Count == 0)
            return Remove;

        // import * as ns from "mod"
        if (importedNames.Count == 1 && importedNames[0].ForeignName.Name == "*" && importName == null)
        {
            var nsName = importedNames[0].Name.Name;
            var statements = new StructRefList<AstNode>();
            statements.Add(MakeConst(import, nsName,
                MakeImportStarCall(import, MakeRequireCall(import, moduleExpr))));
            AddDeferredExportStatements(importedNames[0].Name, ref statements);
            return statements.Count == 1 ? statements[0] : SpreadStructList(ref statements);
        }

        // import def, * as ns from "mod"
        if (importedNames.Count == 1 && importedNames[0].ForeignName.Name == "*" && importName != null)
        {
            var nsName = importedNames[0].Name.Name;
            var modVarName = MakeModuleVarName(moduleExpr);
            RegisterImportReplacement(importName, from => MakeDot(MakeSymbolRef(from, modVarName), "default"));
            var statements = new StructRefList<AstNode>();
            statements.Add(MakeConst(import, modVarName,
                    MakeImportStarCall(import, MakeRequireCall(import, moduleExpr)),
                nsName, MakeSymbolRef(import, modVarName)));
            AddDeferredExportStatements(importName, ref statements);
            AddDeferredExportStatements(importedNames[0].Name, ref statements);
            return statements.Count == 1 ? statements[0] : SpreadStructList(ref statements);
        }

        // import def from "mod"  (no named imports)
        if (importName != null && importedNames.Count == 0)
        {
            var modVarName = MakeModuleVarName(moduleExpr);
            RegisterImportReplacement(importName, from => MakeDot(MakeSymbolRef(from, modVarName), "default"));
            var statements = new StructRefList<AstNode>();
            statements.Add(MakeConst(import, modVarName,
                MakeImportDefaultCall(import, MakeRequireCall(import, moduleExpr))));
            AddDeferredExportStatements(importName, ref statements);
            return statements.Count == 1 ? statements[0] : SpreadStructList(ref statements);
        }

        // import def, { a, b as c } from "mod"  (default + named)
        if (importName != null && importedNames.Count > 0 && importedNames[0].ForeignName.Name != "*")
        {
            var statements = new StructRefList<AstNode>();
            var modVarName = MakeModuleVarName(moduleExpr);
            statements.Add(MakeConst(import, modVarName,
                MakeImportStarCall(import, MakeRequireCall(import, moduleExpr))));

            RegisterImportReplacement(importName, from => MakeDot(MakeSymbolRef(from, modVarName), "default"));

            foreach (var mapping in importedNames.AsReadOnlySpan())
            {
                var foreignName = mapping.ForeignName.Name;
                RegisterImportReplacement(mapping.Name, from => MakeDot(MakeSymbolRef(from, modVarName), foreignName),
                    true, IsRelativeModuleName(moduleExpr));
                AddDeferredExportStatements(mapping.Name, ref statements);
            }

            return SpreadStructList(ref statements);
        }

        // import { a, b as c } from "mod"  (named only, no default)
        if (importName == null && importedNames.Count > 0 && importedNames[0].ForeignName.Name != "*")
        {
            var statements = new StructRefList<AstNode>();
            var modVarName = MakeModuleVarName(moduleExpr);
            var hasDefaultImportMapping = false;
            foreach (var mapping in importedNames.AsReadOnlySpan())
            {
                if (mapping.ForeignName.Name == "default")
                {
                    hasDefaultImportMapping = true;
                    break;
                }
            }

            var requireCall = MakeRequireCall(import, moduleExpr);
            var moduleValue = hasDefaultImportMapping
                ? importedNames.Count == 1
                    ? MakeImportDefaultCall(import, requireCall)
                    : MakeImportStarCall(import, requireCall)
                : requireCall;
            statements.Add(MakeConst(import, modVarName, moduleValue));

            foreach (var mapping in importedNames.AsReadOnlySpan())
            {
                var foreignName = mapping.ForeignName.Name;
                RegisterImportReplacement(mapping.Name, from => MakeDot(MakeSymbolRef(from, modVarName), foreignName),
                    true, IsRelativeModuleName(moduleExpr));
                AddDeferredExportStatements(mapping.Name, ref statements);
            }

            return SpreadStructList(ref statements);
        }

        return import;
    }

    static StructList<AstNameMapping> FilterRuntimeImportMappings(in StructRefList<AstNameMapping> importedNames)
    {
        var result = new StructList<AstNameMapping>();
        foreach (var mapping in importedNames.AsReadOnlySpan())
        {
            if (HasRuntimeReferences(mapping.Name))
                result.Add(mapping);
        }

        return result;
    }

    static bool HasRuntimeReferences(AstSymbol? symbol)
    {
        return symbol?.Thedef?.References.Count > 0;
    }

    AstNode? TransformExport(AstExport export)
    {
        if (export.ExportedNames.Count == 0 && export.ModuleName == null &&
            export.ExportedDefinition == null && export.ExportedValue == null)
            return Remove;

        // export * from "mod"
        if (export.ExportedNames.Count == 1 &&
            export.ExportedNames[0].ForeignName.Name == "*" &&
            export.ExportedNames[0].Name.Name == "*" &&
            export.ModuleName != null)
        {
            var statements = new StructRefList<AstNode>();
            statements.Add(MakeSimpleStatement(export,
                MakeExportStarCall(export, MakeRequireCall(export, export.ModuleName))));
            return SpreadStructList(ref statements);
        }

        // export * as ns from "mod"
        if (export.ExportedNames.Count == 1 &&
            export.ExportedNames[0].Name.Name == "*" &&
            export.ExportedNames[0].ForeignName.Name != "*" &&
            export.ModuleName != null)
        {
            var nsName = export.ExportedNames[0].ForeignName.Name;
            AddExportPreinit(nsName);
            var statements = new StructRefList<AstNode>();
            statements.Add(MakeExportsAssignStatement(export, nsName,
                MakeImportStarCall(export, MakeRequireCall(export, export.ModuleName))));
            return SpreadStructList(ref statements);
        }

        // export { a, b as c } from "mod"  (named re-export)
        if (export.ExportedNames.Count > 0 && export.ModuleName != null &&
            (export.ExportedNames.Count > 1 || export.ExportedNames[0].ForeignName.Name != "*"))
        {
            var statements = new StructRefList<AstNode>();
            var modVarName = MakeModuleVarName(export.ModuleName);
            statements.Add(MakeVar(export, modVarName, MakeRequireCall(export, export.ModuleName)));

            foreach (var mapping in export.ExportedNames.AsReadOnlySpan())
            {
                var importedName = mapping.Name.Name;
                var exportedName = mapping.ForeignName.Name;
                AddExportPreinit(exportedName);
                AstNode value = MakeDot(MakeSymbolRef(export, modVarName), importedName);
                if (mapping.Name.Name == "default")
                    value = MakeDot(MakeImportDefaultCall(export, MakeSymbolRef(export, modVarName)), "default");
                statements.Add(MakeSimpleStatement(export,
                    MakeDefineExportGetterCall(export, exportedName, value)));
            }

            return SpreadStructList(ref statements);
        }

        // export { a, b as c }  (local named export, no from)
        if (export.ExportedNames.Count > 0 && export.ModuleName == null)
        {
            var statements = new StructRefList<AstNode>();
            foreach (var mapping in export.ExportedNames.AsReadOnlySpan())
            {
                if (IsErasedTypeOnlyLocalExport(mapping))
                    continue;
                var localName = mapping.Name.Name;
                var exportedName = mapping.ForeignName.Name;
                AddExportPreinit(exportedName);
                if (mapping.Name.Thedef is { Orig.Count: > 0 } def && def.Orig[0] is not AstSymbolImport)
                {
                    AddDeferredLocalExport(localName, exportedName);
                }
                else if (_includeExportSetters && _consumedDeferredLocalExports.Contains(localName))
                {
                }
                else
                {
                    var value = MakeImportedOrLocalRef(export, mapping.Name);
                    if (mapping.Name.Thedef != null && _liveImportBindings.ContainsKey(mapping.Name.Thedef))
                        statements.Add(MakeSimpleStatement(export,
                            MakeDefineExportGetterCall(export, exportedName, value,
                                _includeExportSetters)));
                    else
                        statements.Add(MakeExportsAssignStatement(export, exportedName, value));
                }
            }

            return statements.Count == 0 ? Remove : SpreadStructList(ref statements);
        }

        // export default expr
        if (export.IsDefault && export.ExportedValue != null)
        {
            if (export.ExportedValue is AstClassExpression classExpression && classExpression.Name == null)
            {
                var defaultName = MakeDefaultExportName();
                var props = new StructRefList<AstNode>();
                props.AddRange(classExpression.Properties.AsReadOnlySpan());
                var statements = new StructRefList<AstNode>();
                statements.Add(new AstDefClass(classExpression.Source, classExpression.Start, classExpression.End,
                    new AstSymbolDefClass(new AstSymbolVar(classExpression, defaultName)), classExpression.Extends, ref props));
                statements.Add(MakeExportsAssignStatement(export, "default", MakeSymbolRef(export, defaultName)));
                return SpreadStructList(ref statements);
            }

            var value = Transform(export.ExportedValue);
            if (value is AstSymbolRef symbolRef)
                value = MakeImportedOrLocalRef(export, symbolRef);
            return MakeSimpleStatement(export,
                MakeExportsAssign(export, "default", value));
        }

        // export default function/class (ExportedDefinition)
        if (export.IsDefault && export.ExportedDefinition != null)
        {
            var statements = new StructRefList<AstNode>();
            if (export.ExportedDefinition is AstDefun defaultDefun && defaultDefun.Name == null)
                defaultDefun.Name = new AstSymbolDefun(new AstSymbolVar(defaultDefun, MakeDefaultExportName()));

            if (export.ExportedDefinition is AstDefClass defaultClass && defaultClass.Name == null)
                defaultClass.Name = new AstSymbolDefClass(new AstSymbolVar(defaultClass, MakeDefaultExportName()));

            var name = GetDefinitionName(export.ExportedDefinition);
            if (export.ExportedDefinition is AstDefun)
                _hoistedExportAssignments.Add(MakeExportsAssignStatement(export, "default", MakeSymbolRef(export, name)));
            statements.Add(Transform(export.ExportedDefinition));
            if (export.ExportedDefinition is not AstDefun)
                statements.Add(MakeExportsAssignStatement(export, "default",
                    MakeSymbolRef(export, name)));
            return SpreadStructList(ref statements);
        }

        // export var/let/const
        if (export.ExportedDefinition is AstDefinitions defs)
        {
            var statements = new StructRefList<AstNode>();
            var keepDefinitions = false;
            foreach (var def in defs.Definitions.AsReadOnlySpan())
            {
                if (def.Name is AstSymbol symbol)
                {
                    AddExportPreinit(symbol.Name);
                    if (def.Value != null)
                    {
                        if (def.Value is AstArrow or AstFunction)
                        {
                            RegisterExportReplacement(symbol);
                            def.Value = Transform(def.Value);
                            keepDefinitions = true;
                            statements.Add(MakeExportsAssignStatement(export, symbol.Name,
                                MakeSymbolRef(export, symbol.Name)));
                        }
                        else
                        {
                            RegisterExportReplacement(symbol);
                            statements.Add(MakeExportsAssignStatement(export, symbol.Name, Transform(def.Value)));
                        }
                    }
                    else
                    {
                        if (symbol.Thedef?.References.Count > 0)
                            keepDefinitions = true;
                    }
                }
                else if (def.Name is AstDestructuring dest)
                {
                    if (def.Value != null && TryAddExportedDestructuring(export, dest, Transform(def.Value),
                            ref statements))
                    {
                        // TypeScript lowers exported destructuring through temporaries that write exports directly.
                    }
                    else
                    {
                        keepDefinitions = true;
                        AddDestructuringExports(export, dest, ref statements);
                    }
                }
            }

            if (keepDefinitions)
                statements.Insert(0, defs);

            return SpreadStructList(ref statements);
        }

        // export function
        if (export.ExportedDefinition is AstDefun defun && defun.Name != null)
        {
            var statements = new StructRefList<AstNode>();
            _hoistedExportAssignments.Add(MakeExportsAssignStatement(export, defun.Name.Name,
                MakeSymbolRef(export, defun.Name.Name)));
            statements.Add(Transform(defun));
            return SpreadStructList(ref statements);
        }

        // export class
        if (export.ExportedDefinition is AstDefClass defClass && defClass.Name != null)
        {
            var statements = new StructRefList<AstNode>();
            AddExportPreinit(defClass.Name.Name);
            statements.Add(Transform(defClass));
            statements.Add(MakeExportsAssignStatement(export, defClass.Name.Name,
                MakeSymbolRef(export, defClass.Name.Name)));
            return SpreadStructList(ref statements);
        }

        return null;
    }

    static bool IsErasedTypeOnlyLocalExport(AstNameMapping mapping)
    {
        return mapping.Name.Thedef == null || mapping.Name.Thedef.Orig.Count == 0;
    }

    AstNode? TransformExportedLocalAssignment(AstAssign assignment)
    {
        if (assignment.Operator != Operator.Assignment ||
            assignment.Left is not AstSymbolRef symbol ||
            !_exportedLocalAssignments.TryGetValue(symbol.Name, out var exportedNames))
            return null;

        AstNode value = assignment;
        foreach (var exportedName in exportedNames)
            value = MakeExportsAssign(assignment, exportedName, value);
        return value;
    }

    void AddDestructuringExports(AstNode from, AstDestructuring dest, ref StructRefList<AstNode> statements)
    {
        foreach (var name in dest.Names.AsReadOnlySpan())
        {
            if (name is AstDestructuring nestedDest)
            {
                AddDestructuringExports(from, nestedDest, ref statements);
            }
            else if (name is AstSymbol symbol)
            {
                AddExportPreinit(symbol.Name);
                statements.Add(MakeExportsAssignStatement(from, symbol.Name,
                    MakeSymbolRef(from, symbol.Name)));
            }
            else if (name is AstNameMapping mapping)
            {
                var exportName = mapping.ForeignName.Name;
                var localName = mapping.Name.Name;
                AddExportPreinit(exportName);
                statements.Add(MakeExportsAssignStatement(from, exportName,
                    MakeSymbolRef(from, localName)));
            }
        }
    }

    bool TryAddExportedDestructuring(AstNode from, AstDestructuring dest, AstNode value,
        ref StructRefList<AstNode> statements)
    {
        if (dest.IsArray)
            return false;

        if (TryAddSingleExportedObjectDestructuring(from, dest, value, ref statements))
            return true;

        var assignments = new List<(string ExportName, string Access)>();
        if (!TryCollectObjectDestructuringExports(dest, "", assignments) || assignments.Count == 0)
            return false;

        var tempName = AllocateTempName();
        var source = new System.Text.StringBuilder();
        source.Append(tempName).Append(" = ").Append(value.PrintToString()).Append(", ");
        for (var i = 0; i < assignments.Count; i++)
        {
            if (i > 0)
                source.Append(", ");
            var (exportName, access) = assignments[i];
            source.Append("exports.").Append(exportName).Append(" = ").Append(tempName).Append(access);
            AddExportPreinit(exportName);
            AddExportedLocalAssignment(exportName, exportName);
        }
        source.Append(';');

        statements.Add(ParseStatements(from, source.ToString())[0]);
        return true;
    }

    bool TryAddSingleExportedObjectDestructuring(AstNode from, AstDestructuring dest, AstNode value,
        ref StructRefList<AstNode> statements)
    {
        if (dest.Names.Count != 1 || !TryGetShorthandObjectDestructuringSymbol(dest.Names[0], out var symbol))
            return false;

        AddExportPreinit(symbol.Name);
        AddExportedLocalAssignment(symbol.Name, symbol.Name);
        statements.Add(MakeExportsAssignStatement(from, symbol.Name,
            new AstDot(value.Source, value.Start, value.End, value, symbol.Name)));
        return true;
    }

    static bool TryGetShorthandObjectDestructuringSymbol(AstNode node, out AstSymbol symbol)
    {
        if (node is AstSymbol direct)
        {
            symbol = direct;
            return true;
        }

        if (node is AstObjectKeyVal { Key: AstSymbol key, Value: AstSymbol value } && key.Name == value.Name)
        {
            symbol = value;
            return true;
        }

        symbol = null!;
        return false;
    }

    static bool TryCollectObjectDestructuringExports(AstDestructuring dest, string prefix,
        List<(string ExportName, string Access)> assignments)
    {
        if (dest.IsArray)
            return false;

        foreach (var name in dest.Names.AsReadOnlySpan())
        {
            switch (name)
            {
                case AstObjectKeyVal keyValue:
                    if (!TryGetPropertyAccess(keyValue.Key, out var propertyAccess))
                        return false;
                    if (!TryCollectDestructuringExportValue(keyValue.Value, prefix + propertyAccess, assignments))
                        return false;
                    break;
                case AstSymbol symbol:
                    assignments.Add((symbol.Name, prefix + "." + symbol.Name));
                    break;
                default:
                    return false;
            }
        }

        return true;
    }

    static bool TryCollectDestructuringExportValue(AstNode value, string access,
        List<(string ExportName, string Access)> assignments)
    {
        switch (value)
        {
            case AstSymbol symbol:
                assignments.Add((symbol.Name, access));
                return true;
            case AstDefaultAssign { Left: AstSymbol symbol }:
                assignments.Add((symbol.Name, access));
                return true;
            case AstDestructuring nested:
                return TryCollectObjectDestructuringExports(nested, access, assignments);
            default:
                return false;
        }
    }

    static bool TryGetPropertyAccess(AstNode key, out string access)
    {
        switch (key)
        {
            case AstSymbol symbol when IsIdentifierName(symbol.Name):
                access = "." + symbol.Name;
                return true;
            case AstString str when IsIdentifierName(str.Value):
                access = "." + str.Value;
                return true;
            case AstString str:
                access = "[" + QuoteJsString(str.Value) + "]";
                return true;
            case AstNumber number:
                access = "[" + number.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]";
                return true;
            default:
                access = "";
                return false;
        }
    }

    static bool IsIdentifierName(string value)
    {
        if (value.Length == 0 || !(char.IsLetter(value[0]) || value[0] == '_' || value[0] == '$'))
            return false;
        for (var i = 1; i < value.Length; i++)
            if (!(char.IsLetterOrDigit(value[i]) || value[i] == '_' || value[i] == '$'))
                return false;
        return true;
    }

    static string GetDefinitionName(AstNode definition)
    {
        return definition switch
        {
            AstDefun defun when defun.Name != null => defun.Name.Name,
            AstDefClass defClass when defClass.Name != null => defClass.Name.Name,
            _ => ""
        };
    }

    AstNode TransformDynamicImport(AstImportExpression importExpr)
    {
        // import(modExpr) → Promise.resolve().then(function() { return __importStar(require(modExpr)); })
        var bodyList = new StructRefList<AstNode>();
        bodyList.Add(MakeImportStarCall(importExpr, MakeRequireCall(importExpr, importExpr.ModuleName)));

        var argNames = new StructRefList<AstNode>();
        var func = new AstArrow(importExpr.Source, importExpr.Start, importExpr.End,
            null, ref argNames, false, false, ref bodyList);

        var thenArgs = new StructRefList<AstNode>();
        thenArgs.Add(func);

        var promiseRef = new AstSymbolRef(importExpr, "Promise");
        var resolveDot = new AstDot(promiseRef, "resolve");
        var resolveCallArgs = new StructRefList<AstNode>();
        var resolveCall = new AstCall(importExpr.Source, importExpr.Start, importExpr.End,
            resolveDot, ref resolveCallArgs);

        var thenDot = new AstDot(resolveCall, "then");
        return MakeCall(importExpr, thenDot, ref thenArgs);
    }

    // ── helper AST constructors ──────────────────────────────────────

    static AstSimpleStatement MakeSimpleStatement(AstNode from, AstNode expr)
    {
        return new AstSimpleStatement(from.Source, from.Start, from.End, expr);
    }

    static AstVar MakeVar(AstNode from, string name, AstNode? value)
    {
        var varDef = new AstVarDef(from, new AstSymbolVar(from, name), value);
        var varDecl = new AstVar(from);
        varDecl.Definitions.Add(varDef);
        return varDecl;
    }

    AstVar MakeTempVarStatement(AstNode from)
    {
        var definitions = new StructRefList<AstVarDef>();
        foreach (var name in _hoistedTempNames)
            definitions.Add(new AstVarDef(from, new AstSymbolVar(from, name), null));
        return new AstVar(from.Source, from.Start, from.End, ref definitions);
    }

    static AstConst MakeConst(AstNode from, string name, AstNode? value)
    {
        var definitions = new StructRefList<AstVarDef>();
        definitions.Add(new AstVarDef(from, new AstSymbolConst(new AstSymbolVar(from, name)), value));
        return new AstConst(from.Source, from.Start, from.End, ref definitions);
    }

    static AstConst MakeConst(AstNode from, string name, AstNode? value, string name2, AstNode? value2)
    {
        var definitions = new StructRefList<AstVarDef>();
        definitions.Add(new AstVarDef(from, new AstSymbolConst(new AstSymbolVar(from, name)), value));
        definitions.Add(new AstVarDef(from, new AstSymbolConst(new AstSymbolVar(from, name2)), value2));
        return new AstConst(from.Source, from.Start, from.End, ref definitions);
    }

    string MakeModuleVarName(AstNode moduleExpr)
    {
        if (moduleExpr is not AstString moduleName)
            return "mod_" + (++_moduleVarCounter);

        var name = moduleName.Value;
        var slash = Math.Max(name.LastIndexOf('/'), name.LastIndexOf('\\'));
        if (slash >= 0)
            name = name[(slash + 1)..];
        var sanitized = new string(name.Select((ch, i) =>
        {
            if (char.IsLetterOrDigit(ch) || ch == '_' || ch == '$')
                return ch;
            return '_';
        }).ToArray());
        if (sanitized.Length > 0 && char.IsDigit(sanitized[0]))
            sanitized = "_" + sanitized;
        if (sanitized.Length == 0)
            sanitized = "mod";
        name = sanitized;
        _moduleNameCounters.TryGetValue(name, out var counter);
        counter++;
        _moduleNameCounters[name] = counter;
        return name + "_" + counter;
    }

    string MakeDefaultExportName()
    {
        return "default_" + (++_defaultExportCounter);
    }

    string AllocateTempName()
    {
        _tempVarCounter++;
        var index = _tempVarCounter;
        if (index >= 9)
            index++;
        var name = "_" + (char)('a' + index - 1);
        _hoistedTempNames.Add(name);
        return name;
    }

    void AddExportPreinit(string name)
    {
        if (!_exportPreinitNames.Contains(name))
            _exportPreinitNames.Add(name);
    }

    void RegisterImportReplacement(AstSymbol symbol, Func<AstNode, AstNode> replacement, bool liveBinding = false,
        bool chainExportSetter = false)
    {
        _unresolvedImportReferenceReplacements[symbol.Name] = replacement;
        if (symbol.Thedef != null)
        {
            _importReferenceReplacements[symbol.Thedef] = replacement;
            if (liveBinding)
                _liveImportBindings[symbol.Thedef] = chainExportSetter;
        }
    }

    static bool IsRelativeModuleName(AstNode moduleExpr)
    {
        return moduleExpr is AstString { Value: { } value } &&
               (value.StartsWith(".", StringComparison.Ordinal) || value.StartsWith("/", StringComparison.Ordinal));
    }

    void RegisterExportReplacement(AstSymbol symbol)
    {
        if (symbol.Thedef != null)
            _importReferenceReplacements[symbol.Thedef] = from => MakeDot(new AstSymbolRef(from, "exports"), symbol.Name);
    }

    AstNode MakeImportedOrLocalRef(AstNode from, AstSymbol symbol)
    {
        if (symbol.Thedef != null && _importReferenceReplacements.TryGetValue(symbol.Thedef, out var replacement))
            return replacement(from);
        return MakeSymbolRef(from, symbol.Name);
    }

    static AstAssign MakeExportsAssign(AstNode from, string propName, AstNode value)
    {
        var exportsRef = new AstSymbolRef(from, "exports");
        var dot = new AstDot(exportsRef, propName);
        return new AstAssign(from.Source, from.Start, from.End, dot, value, Operator.Assignment);
    }

    static AstSimpleStatement MakeExportsAssignStatement(AstNode from, string propName, AstNode value)
    {
        return MakeSimpleStatement(from, MakeExportsAssign(from, propName, value));
    }

    void AddExportsPreinitStatements(AstNode from, ref StructRefList<AstNode> statements)
    {
        if (_exportPreinitNames.Count <= ExportPreinitChunkSize)
        {
            statements.Add(MakeExportsPreinitStatement(from, _exportPreinitNames));
            return;
        }

        for (var offset = 0; offset < _exportPreinitNames.Count; offset += ExportPreinitChunkSize)
        {
            var count = Math.Min(ExportPreinitChunkSize, _exportPreinitNames.Count - offset);
            statements.Add(MakeExportsPreinitStatement(from,
                _exportPreinitNames.GetRange(offset, count)));
        }
    }

    AstSimpleStatement MakeExportsPreinitStatement(AstNode from, IReadOnlyList<string> names)
    {
        AstNode value = MakeVoidZero(from);
        for (var i = 0; i < names.Count; i++)
            value = MakeExportsAssign(from, names[i], value);
        return MakeSimpleStatement(from, value);
    }

    static AstUnaryPrefix MakeVoidZero(AstNode from)
    {
        return new AstUnaryPrefix(from.Source, from.Start, from.End, Operator.Void,
            new AstNumber(from.Source, from.Start, from.End, 0, "0"));
    }

    static AstCall MakeRequireCall(AstNode from, AstNode moduleExpr)
    {
        var requireRef = new AstSymbolRef(from, "require");
        var args = new StructRefList<AstNode>();
        args.Add(moduleExpr);
        return MakeCall(from, requireRef, ref args);
    }

    static AstCall MakeCall(AstNode from, AstNode expression, ref StructRefList<AstNode> args)
    {
        return new AstCall(from.Source, from.Start, from.End, expression, ref args);
    }

    static AstCall MakeImportDefaultCall(AstNode from, AstNode arg)
    {
        var importDefaultRef = new AstSymbolRef(from, "__importDefault");
        var args = new StructRefList<AstNode>();
        args.Add(arg);
        return MakeCall(from, importDefaultRef, ref args);
    }

    static AstCall MakeImportStarCall(AstNode from, AstNode arg)
    {
        var importStarRef = new AstSymbolRef(from, "__importStar");
        var args = new StructRefList<AstNode>();
        args.Add(arg);
        return MakeCall(from, importStarRef, ref args);
    }

    static AstDot MakeDot(AstNode expr, string propName)
    {
        return new AstDot(expr, propName);
    }

    static AstSymbolRef MakeSymbolRef(AstNode from, string name)
    {
        return new AstSymbolRef(from, name);
    }

    static AstCall MakeExportStarCall(AstNode from, AstNode modRef)
    {
        var exportStarRef = new AstSymbolRef(from, "__exportStar");
        var exportsRef = new AstSymbolRef(from, "exports");
        var args = new StructRefList<AstNode>();
        args.Add(modRef);
        args.Add(exportsRef);
        return MakeCall(from, exportStarRef, ref args);
    }

    AstCall MakeDefineExportGetterCall(AstNode from, string exportedName, AstNode value)
    {
        return MakeDefineExportGetterCall(from, exportedName, value, false);
    }

    AstCall MakeDefineExportGetterCall(AstNode from, string exportedName, AstNode value, bool chainSetter)
    {
        var includeSetter = _includeExportSetters;
        var descriptor = includeSetter
            ? ", { enumerable: true, get: function () { return __njsastValue; }, set: function (v) { __njsastValue = v; } });"
            : ", { enumerable: true, get: function () { return __njsastValue; } });";
        var source = "Object.defineProperty(exports, " + QuoteJsString(exportedName) + descriptor;
        var statement = ParseStatements(from, source)[0];
        var call = (AstCall)((AstSimpleStatement)statement).Body;
        var obj = (AstObject)call.Args[2];
        var getter = (AstObjectKeyVal)obj.Properties[1];
        ((AstFunction)getter.Value).Body.SetItem(0, new AstReturn(includeSetter ? value.DeepClone() : value));
        if (includeSetter)
        {
            var setter = (AstObjectKeyVal)obj.Properties[2];
            var setterBody = (AstSimpleStatement)((AstFunction)setter.Value).Body[0];
            var assign = (AstAssign)setterBody.Body;
            if (chainSetter)
                setterBody.Body = MakeExportsAssign(from, exportedName,
                    new AstAssign(value, assign.Right, Operator.Assignment));
            else
                assign.Left = value;
        }
        return call;
    }

    static string QuoteJsString(string value)
    {
        return "\"" + value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    static StructRefList<AstNode> ParseStatements(AstNode from, string source)
    {
        var parser = new Parser(new Options
        {
            EcmaVersion = 2022,
            SourceType = SourceType.Script,
            SourceFile = from.Source
        }, source);
        return parser.Parse().Body;
    }

}
