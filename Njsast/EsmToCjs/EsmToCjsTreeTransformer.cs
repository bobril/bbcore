using System;
using Njsast.Ast;
using Njsast.Reader;

namespace Njsast.EsmToCjs;

public class EsmToCjsTreeTransformer : TreeTransformer
{
    uint _moduleVarCounter;

    protected override AstNode? Before(AstNode node, bool inList)
    {
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
            var marker = new AstNode[] { MakeEsModuleMarker(toplevel) };
            toplevel.Body.InsertRange(0, new ReadOnlySpan<AstNode>(marker));
        }

        return null;
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
            return MakeVar(import, nsName, MakeRequireCall(import, moduleExpr));
        }

        // import def, * as ns from "mod"
        if (importedNames.Count == 1 && importedNames[0].ForeignName.Name == "*" && importName != null)
        {
            var nsName = importedNames[0].Name.Name;
            var defName = importName.Name;
            var statements = new StructList<AstNode>();

            var requireCall = MakeRequireCall(import, moduleExpr);
            statements.Add(MakeVar(import, nsName, requireCall));

            statements.Add(MakeVar(import, defName,
                MakeImportDefaultCall(import, MakeSymbolRef(import, nsName))));

            return SpreadStructList(ref statements);
        }

        // import def from "mod"  (no named imports)
        if (importName != null && importedNames.Count == 0)
        {
            return MakeVar(import, importName.Name,
                MakeImportDefaultCall(import, MakeRequireCall(import, moduleExpr)));
        }

        // import def, { a, b as c } from "mod"  (default + named)
        if (importName != null && importedNames.Count > 0 && importedNames[0].ForeignName.Name != "*")
        {
            var statements = new StructList<AstNode>();

            statements.Add(MakeVar(import, importName.Name,
                MakeImportDefaultCall(import, MakeRequireCall(import, moduleExpr))));

            foreach (var mapping in importedNames.AsReadOnlySpan())
            {
                var localName = mapping.Name.Name;
                var foreignName = mapping.ForeignName.Name;
                statements.Add(MakeVar(import, localName,
                    MakeDot(MakeSymbolRef(import, importName.Name), foreignName)));
            }

            return SpreadStructList(ref statements);
        }

        // import { a, b as c } from "mod"  (named only, no default)
        if (importName == null && importedNames.Count > 0 && importedNames[0].ForeignName.Name != "*")
        {
            var statements = new StructList<AstNode>();
            var modVarName = "_mod" + (++_moduleVarCounter);
            statements.Add(MakeVar(import, modVarName, MakeRequireCall(import, moduleExpr)));

            foreach (var mapping in importedNames.AsReadOnlySpan())
            {
                var localName = mapping.Name.Name;
                var foreignName = mapping.ForeignName.Name;
                statements.Add(MakeVar(import, localName,
                    MakeDot(MakeSymbolRef(import, modVarName), foreignName)));
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
            var statements = new StructList<AstNode>();
            var modVarName = "_mod" + (++_moduleVarCounter);
            statements.Add(MakeVar(export, modVarName, MakeRequireCall(export, export.ModuleName)));
            statements.Add(MakeSimpleStatement(export,
                MakeExportStarCall(export, MakeSymbolRef(export, modVarName))));
            return SpreadStructList(ref statements);
        }

        // export * as ns from "mod"
        if (export.ExportedNames.Count == 1 &&
            export.ExportedNames[0].ForeignName.Name == "*" &&
            export.ExportedNames[0].Name.Name != "*" &&
            export.ModuleName != null)
        {
            var nsName = export.ExportedNames[0].Name.Name;
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
            var modVarName = "_mod" + (++_moduleVarCounter);
            statements.Add(MakeVar(export, modVarName, MakeRequireCall(export, export.ModuleName)));

            foreach (var mapping in export.ExportedNames.AsReadOnlySpan())
            {
                var importedName = mapping.ForeignName.Name;
                var exportedName = mapping.Name.Name;
                statements.Add(MakeSimpleStatement(export,
                    MakeCreateBindingCall(export, MakeSymbolRef(export, modVarName), importedName, exportedName)));
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
                statements.Add(MakeExportsAssignStatement(export, exportedName,
                    MakeSymbolRef(export, localName)));
            }

            return SpreadStructList(ref statements);
        }

        // export default expr
        if (export.IsDefault && export.ExportedValue != null)
        {
            return MakeSimpleStatement(export,
                MakeExportsAssign(export, "default", export.ExportedValue));
        }

        // export default function/class (ExportedDefinition)
        if (export.IsDefault && export.ExportedDefinition != null)
        {
            var statements = new StructList<AstNode>();
            statements.Add(export.ExportedDefinition);
            var name = GetDefinitionName(export.ExportedDefinition);
            statements.Add(MakeExportsAssignStatement(export, "default",
                MakeSymbolRef(export, name)));
            return SpreadStructList(ref statements);
        }

        // export var/let/const
        if (export.ExportedDefinition is AstDefinitions defs)
        {
            var statements = new StructList<AstNode>();
            statements.Add(defs);
            foreach (var def in defs.Definitions.AsReadOnlySpan())
            {
                if (def.Name is AstSymbol symbol)
                {
                    statements.Add(MakeExportsAssignStatement(export, symbol.Name,
                        MakeSymbolRef(export, symbol.Name)));
                }
                else if (def.Name is AstDestructuring dest)
                {
                    AddDestructuringExports(export, dest, ref statements);
                }
            }

            return SpreadStructList(ref statements);
        }

        // export function
        if (export.ExportedDefinition is AstDefun defun && defun.Name != null)
        {
            var statements = new StructList<AstNode>();
            statements.Add(defun);
            statements.Add(MakeExportsAssignStatement(export, defun.Name.Name,
                MakeSymbolRef(export, defun.Name.Name)));
            return SpreadStructList(ref statements);
        }

        // export class
        if (export.ExportedDefinition is AstDefClass defClass && defClass.Name != null)
        {
            var statements = new StructList<AstNode>();
            statements.Add(defClass);
            statements.Add(MakeExportsAssignStatement(export, defClass.Name.Name,
                MakeSymbolRef(export, defClass.Name.Name)));
            return SpreadStructList(ref statements);
        }

        return null;
    }

    static void AddDestructuringExports(AstNode from, AstDestructuring dest, ref StructList<AstNode> statements)
    {
        foreach (var name in dest.Names.AsReadOnlySpan())
        {
            if (name is AstDestructuring nestedDest)
            {
                AddDestructuringExports(from, nestedDest, ref statements);
            }
            else if (name is AstSymbol symbol)
            {
                statements.Add(MakeExportsAssignStatement(from, symbol.Name,
                    MakeSymbolRef(from, symbol.Name)));
            }
            else if (name is AstNameMapping mapping)
            {
                var exportName = mapping.ForeignName.Name;
                var localName = mapping.Name.Name;
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
        var requireCall = MakeRequireCall(importExpr, importExpr.ModuleName);
        var returnStmt = new AstReturn(requireCall);
        var bodyList = new StructList<AstNode>();
        bodyList.Add(returnStmt);

        var argNames = new StructList<AstNode>();
        var func = new AstFunction(importExpr.Source, importExpr.Start, importExpr.End,
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

    static AstCall MakeCreateBindingCall(AstNode from, AstNode modRef, string importedName, string exportedName)
    {
        var createBindingRef = new AstSymbolRef(from, "__createBinding");
        var exportsRef = new AstSymbolRef(from, "exports");
        var args = new StructList<AstNode>();
        args.Add(exportsRef);
        args.Add(modRef);
        args.Add(new AstString(importedName));
        if (exportedName != importedName)
        {
            args.Add(new AstString(exportedName));
        }

        return MakeCall(from, createBindingRef, ref args);
    }

    static AstSimpleStatement MakeEsModuleMarker(AstNode from)
    {
        var objRef = new AstSymbolRef(from, "Object");
        var definePropDot = new AstDot(objRef, "defineProperty");
        var exportsRef = new AstSymbolRef(from, "exports");
        var esModuleStr = new AstString("__esModule");

        var valueTrue = new AstObjectKeyVal(
            new AstString("value"),
            new AstTrue(from.Source, from.Start, from.End));

        var properties = new StructList<AstObjectItem>();
        properties.Add(valueTrue);
        var obj = new AstObject(from.Source, from.Start, from.End, ref properties);

        var args = new StructList<AstNode>();
        args.Add(exportsRef);
        args.Add(esModuleStr);
        args.Add(obj);

        var call = new AstCall(from.Source, from.Start, from.End, definePropDot, ref args);
        return new AstSimpleStatement(from.Source, from.Start, from.End, call);
    }
}
