using System;
using System.Collections.Generic;
using Njsast.Ast;
using Njsast.Output;
using Njsast.Reader;

namespace Njsast.EsmToCjs;

public class EsmToCjsTreeTransformer : TreeTransformer
{
    uint _moduleVarCounter;
    uint _defaultExportCounter;
    bool _needsExportStarHelper;
    readonly Dictionary<SymbolDef, Func<AstNode, AstNode>> _importReferenceReplacements = new();
    readonly HashSet<SymbolDef> _liveImportBindings = new();
    readonly Dictionary<string, uint> _moduleNameCounters = new();
    readonly Dictionary<string, List<string>> _deferredLocalExports = new();
    readonly List<string> _exportPreinitNames = new();
    readonly List<AstNode> _hoistedExportAssignments = new();

    protected override AstNode? Before(AstNode node, bool inList)
    {
        if (node is AstSymbolRef symbolRef && symbolRef.Thedef != null &&
            _importReferenceReplacements.TryGetValue(symbolRef.Thedef, out var replacement))
            return replacement(symbolRef);

        return node switch
        {
            AstImport import => TransformImport(import),
            AstExport export => TransformExport(export),
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
        var prepend = new StructList<AstNode>();
        if (_needsExportStarHelper)
        {
            foreach (var helper in ParseStatements(toplevel, TsExportStarHelpers))
                prepend.Add(helper);
        }

        if (_exportPreinitNames.Count > 0)
            prepend.Add(MakeExportsPreinitStatement(toplevel));

        foreach (var assignment in _hoistedExportAssignments)
            prepend.Add(assignment);

        InsertDeferredLocalExports(toplevel);

        if (prepend.Count == 0)
            return;

        for (var i = (int)prepend.Count - 1; i >= 0; i--)
            toplevel.Body.Insert(0) = prepend[(uint)i];
    }

    void InsertDeferredLocalExports(AstToplevel toplevel)
    {
        for (var i = 0; i < toplevel.Body.Count; i++)
        {
            var statements = new StructList<AstNode>();
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
                toplevel.Body.Insert((int)i + 1 + j) = statements[j];
            i += (int)statements.Count;
        }
    }

    void AddDeferredExportStatements(AstSymbol symbol, ref StructList<AstNode> statements)
    {
        if (!_deferredLocalExports.TryGetValue(symbol.Name, out var exportedNames))
            return;

        foreach (var exportedName in exportedNames)
            statements.Add(MakeExportsAssignStatement(symbol, exportedName, MakeSymbolRef(symbol, symbol.Name)));
    }

    AstNode TransformImport(AstImport import)
    {
        var moduleExpr = import.ModuleName;
        var importName = import.ImportedName;
        var importedNames = import.ImportedNames;

        // import "mod" — side effect only
        if (importName == null && importedNames.Count == 0)
        {
            return MakeSimpleStatement(import, MakeRequireCall(import, moduleExpr));
        }

        // import * as ns from "mod"
        if (importedNames.Count == 1 && importedNames[0].ForeignName.Name == "*" && importName == null)
        {
            var nsName = importedNames[0].Name.Name;
            return MakeConst(import, nsName, MakeRequireCall(import, moduleExpr));
        }

        // import def, * as ns from "mod"
        if (importedNames.Count == 1 && importedNames[0].ForeignName.Name == "*" && importName != null)
        {
            var nsName = importedNames[0].Name.Name;
            var modVarName = MakeModuleVarName(moduleExpr);
            RegisterImportReplacement(importName, from => MakeDot(MakeSymbolRef(from, modVarName), "default"));
            return MakeConst(import, modVarName, MakeRequireCall(import, moduleExpr), nsName, MakeSymbolRef(import, modVarName));
        }

        // import def from "mod"  (no named imports)
        if (importName != null && importedNames.Count == 0)
        {
            var modVarName = MakeModuleVarName(moduleExpr);
            RegisterImportReplacement(importName, from => MakeDot(MakeSymbolRef(from, modVarName), "default"));
            return MakeConst(import, modVarName, MakeRequireCall(import, moduleExpr));
        }

        // import def, { a, b as c } from "mod"  (default + named)
        if (importName != null && importedNames.Count > 0 && importedNames[0].ForeignName.Name != "*")
        {
            var statements = new StructList<AstNode>();
            var modVarName = MakeModuleVarName(moduleExpr);
            statements.Add(MakeConst(import, modVarName, MakeRequireCall(import, moduleExpr)));

            RegisterImportReplacement(importName, from => MakeDot(MakeSymbolRef(from, modVarName), "default"));

            foreach (var mapping in importedNames.AsReadOnlySpan())
            {
                var foreignName = mapping.ForeignName.Name;
                RegisterImportReplacement(mapping.Name, from => MakeDot(MakeSymbolRef(from, modVarName), foreignName), true);
            }

            return SpreadStructList(ref statements);
        }

        // import { a, b as c } from "mod"  (named only, no default)
        if (importName == null && importedNames.Count > 0 && importedNames[0].ForeignName.Name != "*")
        {
            var statements = new StructList<AstNode>();
            var modVarName = MakeModuleVarName(moduleExpr);
            statements.Add(MakeConst(import, modVarName, MakeRequireCall(import, moduleExpr)));

            foreach (var mapping in importedNames.AsReadOnlySpan())
            {
                var foreignName = mapping.ForeignName.Name;
                RegisterImportReplacement(mapping.Name, from => MakeDot(MakeSymbolRef(from, modVarName), foreignName), true);
            }

            return SpreadStructList(ref statements);
        }

        return import;
    }

    AstNode? TransformExport(AstExport export)
    {
        // export * from "mod"
        if (export.ExportedNames.Count == 1 &&
            export.ExportedNames[0].ForeignName.Name == "*" &&
            export.ExportedNames[0].Name.Name == "*" &&
            export.ModuleName != null)
        {
            _needsExportStarHelper = true;
            var statements = new StructList<AstNode>();
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
            var statements = new StructList<AstNode>();
            statements.Add(MakeExportsAssignStatement(export, nsName,
                MakeRequireCall(export, export.ModuleName)));
            return SpreadStructList(ref statements);
        }

        // export { a, b as c } from "mod"  (named re-export)
        if (export.ExportedNames.Count > 0 && export.ModuleName != null &&
            (export.ExportedNames.Count > 1 || export.ExportedNames[0].ForeignName.Name != "*"))
        {
            var statements = new StructList<AstNode>();
            var modVarName = MakeModuleVarName(export.ModuleName);
            statements.Add(MakeVar(export, modVarName, MakeRequireCall(export, export.ModuleName)));

            foreach (var mapping in export.ExportedNames.AsReadOnlySpan())
            {
                var importedName = mapping.Name.Name;
                var exportedName = mapping.ForeignName.Name;
                AddExportPreinit(exportedName);
                statements.Add(MakeSimpleStatement(export,
                    MakeDefineExportGetterCall(export, exportedName, MakeDot(MakeSymbolRef(export, modVarName), importedName))));
            }

            return SpreadStructList(ref statements);
        }

        // export { a, b as c }  (local named export, no from)
        if (export.ExportedNames.Count > 0 && export.ModuleName == null)
        {
            var statements = new StructList<AstNode>();
            foreach (var mapping in export.ExportedNames.AsReadOnlySpan())
            {
                var localName = mapping.Name.Name;
                var exportedName = mapping.ForeignName.Name;
                AddExportPreinit(exportedName);
                if (mapping.Name.Thedef is { Orig.Count: > 0 } def && def.Orig[0] is not AstSymbolImport)
                {
                    if (!_deferredLocalExports.TryGetValue(localName, out var exportedNames))
                    {
                        exportedNames = new List<string>();
                        _deferredLocalExports.Add(localName, exportedNames);
                    }

                    exportedNames.Add(exportedName);
                }
                else
                {
                    var value = MakeImportedOrLocalRef(export, mapping.Name);
                    if (mapping.Name.Thedef != null && _liveImportBindings.Contains(mapping.Name.Thedef))
                        statements.Add(MakeSimpleStatement(export, MakeDefineExportGetterCall(export, exportedName, value)));
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
                var props = new StructList<AstNode>(classExpression.Properties);
                var statements = new StructList<AstNode>();
                statements.Add(new AstDefClass(classExpression.Source, classExpression.Start, classExpression.End,
                    new AstSymbolDefClass(new AstSymbolVar(classExpression, defaultName)), classExpression.Extends, ref props));
                statements.Add(MakeExportsAssignStatement(export, "default", MakeSymbolRef(export, defaultName)));
                return SpreadStructList(ref statements);
            }

            return MakeSimpleStatement(export,
                MakeExportsAssign(export, "default", export.ExportedValue));
        }

        // export default function/class (ExportedDefinition)
        if (export.IsDefault && export.ExportedDefinition != null)
        {
            var statements = new StructList<AstNode>();
            if (export.ExportedDefinition is AstDefun defaultDefun && defaultDefun.Name == null)
                defaultDefun.Name = new AstSymbolDefun(new AstSymbolVar(defaultDefun, MakeDefaultExportName()));

            if (export.ExportedDefinition is AstDefClass defaultClass && defaultClass.Name == null)
                defaultClass.Name = new AstSymbolDefClass(new AstSymbolVar(defaultClass, MakeDefaultExportName()));

            var name = GetDefinitionName(export.ExportedDefinition);
            if (export.ExportedDefinition is AstDefun)
                statements.Add(MakeExportsAssignStatement(export, "default", MakeSymbolRef(export, name)));
            statements.Add(export.ExportedDefinition);
            if (export.ExportedDefinition is not AstDefun)
                statements.Add(MakeExportsAssignStatement(export, "default",
                    MakeSymbolRef(export, name)));
            return SpreadStructList(ref statements);
        }

        // export var/let/const
        if (export.ExportedDefinition is AstDefinitions defs)
        {
            var statements = new StructList<AstNode>();
            var keepDefinitions = false;
            foreach (var def in defs.Definitions.AsReadOnlySpan())
            {
                if (def.Name is AstSymbol symbol)
                {
                    AddExportPreinit(symbol.Name);
                    if (def.Value != null)
                    {
                        statements.Add(MakeExportsAssignStatement(export, symbol.Name, Transform(def.Value)));
                    }
                    else
                    {
                        keepDefinitions = true;
                    }
                }
                else if (def.Name is AstDestructuring dest)
                {
                    keepDefinitions = true;
                    AddDestructuringExports(export, dest, ref statements);
                }
            }

            if (keepDefinitions)
                statements.Insert(0) = defs;

            return SpreadStructList(ref statements);
        }

        // export function
        if (export.ExportedDefinition is AstDefun defun && defun.Name != null)
        {
            var statements = new StructList<AstNode>();
            _hoistedExportAssignments.Add(MakeExportsAssignStatement(export, defun.Name.Name,
                MakeSymbolRef(export, defun.Name.Name)));
            statements.Add(defun);
            return SpreadStructList(ref statements);
        }

        // export class
        if (export.ExportedDefinition is AstDefClass defClass && defClass.Name != null)
        {
            var statements = new StructList<AstNode>();
            AddExportPreinit(defClass.Name.Name);
            statements.Add(defClass);
            statements.Add(MakeExportsAssignStatement(export, defClass.Name.Name,
                MakeSymbolRef(export, defClass.Name.Name)));
            return SpreadStructList(ref statements);
        }

        return null;
    }

    void AddDestructuringExports(AstNode from, AstDestructuring dest, ref StructList<AstNode> statements)
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
        // import(modExpr) → Promise.resolve().then(function() { return require(modExpr); })
        var bodyList = new StructList<AstNode>();
        bodyList.Add(MakeRequireCall(importExpr, importExpr.ModuleName));

        var argNames = new StructList<AstNode>();
        var func = new AstArrow(importExpr.Source, importExpr.Start, importExpr.End,
            null, ref argNames, false, false, ref bodyList);

        var thenArgs = new StructList<AstNode>();
        thenArgs.Add(func);

        var promiseRef = new AstSymbolRef(importExpr, "Promise");
        var resolveDot = new AstDot(promiseRef, "resolve");
        var resolveCallArgs = new StructList<AstNode>();
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

    static AstConst MakeConst(AstNode from, string name, AstNode? value)
    {
        var definitions = new StructList<AstVarDef>();
        definitions.Add(new AstVarDef(from, new AstSymbolConst(new AstSymbolVar(from, name)), value));
        return new AstConst(from.Source, from.Start, from.End, ref definitions);
    }

    static AstConst MakeConst(AstNode from, string name, AstNode? value, string name2, AstNode? value2)
    {
        var definitions = new StructList<AstVarDef>();
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
        var dot = name.LastIndexOf('.');
        if (dot > 0)
            name = name[..dot];
        if (name.Length == 0 || !OutputContext.IsIdentifierString(name) || !OutputContext.IsIdentifier(name))
            name = "mod";

        _moduleNameCounters.TryGetValue(name, out var counter);
        counter++;
        _moduleNameCounters[name] = counter;
        return name + "_" + counter;
    }

    string MakeDefaultExportName()
    {
        return "default_" + (++_defaultExportCounter);
    }

    void AddExportPreinit(string name)
    {
        if (!_exportPreinitNames.Contains(name))
            _exportPreinitNames.Add(name);
    }

    void RegisterImportReplacement(AstSymbol symbol, Func<AstNode, AstNode> replacement, bool liveBinding = false)
    {
        if (symbol.Thedef != null)
        {
            _importReferenceReplacements[symbol.Thedef] = replacement;
            if (liveBinding)
                _liveImportBindings.Add(symbol.Thedef);
        }
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

    AstSimpleStatement MakeExportsPreinitStatement(AstNode from)
    {
        AstNode value = new AstUnaryPrefix(from.Source, from.Start, from.End, Operator.Void,
            new AstNumber(from.Source, from.Start, from.End, 0, "0"));
        for (var i = 0; i < _exportPreinitNames.Count; i++)
            value = MakeExportsAssign(from, _exportPreinitNames[i], value);
        return MakeSimpleStatement(from, value);
    }

    static AstCall MakeRequireCall(AstNode from, AstNode moduleExpr)
    {
        var requireRef = new AstSymbolRef(from, "require");
        var args = new StructList<AstNode>();
        args.Add(moduleExpr);
        return MakeCall(from, requireRef, ref args);
    }

    static AstCall MakeCall(AstNode from, AstNode expression, ref StructList<AstNode> args)
    {
        return new AstCall(from.Source, from.Start, from.End, expression, ref args);
    }

    static AstCall MakeImportDefaultCall(AstNode from, AstNode arg)
    {
        var importDefaultRef = new AstSymbolRef(from, "__importDefault");
        var args = new StructList<AstNode>();
        args.Add(arg);
        return MakeCall(from, importDefaultRef, ref args);
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
        var args = new StructList<AstNode>();
        args.Add(modRef);
        args.Add(exportsRef);
        return MakeCall(from, exportStarRef, ref args);
    }

    static AstCall MakeDefineExportGetterCall(AstNode from, string exportedName, AstNode value)
    {
        var source = "Object.defineProperty(exports, " + QuoteJsString(exportedName) +
                     ", { enumerable: true, get: function () { return __njsastValue; } });";
        var statement = ParseStatements(from, source)[0];
        var call = (AstCall)((AstSimpleStatement)statement).Body;
        var obj = (AstObject)call.Args[2];
        var getter = (AstObjectKeyVal)obj.Properties[1];
        ((AstFunction)getter.Value).Body[0] = new AstReturn(value);
        return call;
    }

    static string QuoteJsString(string value)
    {
        return "\"" + value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    static StructList<AstNode> ParseStatements(AstNode from, string source)
    {
        var parser = new Parser(new Options
        {
            EcmaVersion = 2022,
            SourceType = SourceType.Script,
            SourceFile = from.Source
        }, source);
        return parser.Parse().Body;
    }

    const string TsExportStarHelpers = """
var __createBinding = (this && this.__createBinding) || (Object.create ? (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    var desc = Object.getOwnPropertyDescriptor(m, k);
    if (!desc || ("get" in desc ? !m.__esModule : desc.writable || desc.configurable)) {
      desc = { enumerable: true, get: function() { return m[k]; } };
    }
    Object.defineProperty(o, k2, desc);
}) : (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    o[k2] = m[k];
}));
var __exportStar = (this && this.__exportStar) || function(m, exports) {
    for (var p in m) if (p !== "default" && !Object.prototype.hasOwnProperty.call(exports, p)) __createBinding(exports, m, p);
};
""";

}
