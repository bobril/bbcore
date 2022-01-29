using System;
using System.Collections.Generic;
using Njsast.Ast;
using Njsast.Reader;

namespace Njsast.Bundler;

public class ImportExportTransformer : TreeTransformer
{
    readonly SourceFile _sourceFile;
    readonly Func<string, string, string> _resolver;
    readonly Dictionary<string, SymbolDef> _exportName2VarNameMap = new Dictionary<string, SymbolDef>();
    StructList<AstNode> _bodyPrepend;
    SymbolDef? _reexportSymbol;

    readonly Dictionary<SymbolDef, (string, string[])> _reqSymbolDefMap =
        new Dictionary<SymbolDef, (string, string[])>();

    public (string, string[])? DetectImport(AstNode? node)
    {
        switch (node)
        {
            case AstCall _ when node.IsRequireCall() is { } reqName:
            {
                var resolvedName = _resolver(_sourceFile.Name, reqName);
                _sourceFile.Requires.AddUnique(resolvedName);
                return (resolvedName, Array.Empty<string>());
            }
            case AstSymbolRef symbolRef when _reqSymbolDefMap.TryGetValue(symbolRef.Thedef!, out var res):
                return res;
            case AstPropAccess propAccess when propAccess.PropertyAsString is {} propName &&
                                               DetectImport(propAccess.Expression) is {} leftImport:
                return (leftImport.Item1, Concat(leftImport.Item2, propName));
        }
        return null;
    }

    static string[] Concat(string[] left, string right)
    {
        var leftLength = left.Length;
        var res = new string[leftLength + 1];
        left.AsSpan().CopyTo(res);
        res[leftLength] = right;
        return res;
    }

    public ImportExportTransformer(SourceFile sourceFile, Func<string, string, string> resolver)
    {
        _sourceFile = sourceFile;
        _resolver = resolver;
    }

    protected override AstNode? Before(AstNode node, bool inList)
    {
        var req = node.IsLazyImportCall();
        if (req != null)
        {
            var importedName = _resolver.Invoke(_sourceFile.Name, req);
            _sourceFile.LazyRequires.AddUnique(importedName);
            return node;
        }

        if (node is AstVarDef varDef && varDef.Name.IsSymbolDef() is {} reqSymbolDef && reqSymbolDef.IsSingleInit)
        {
            if (DetectImport(varDef.Value) is { } import)
            {
                _reqSymbolDefMap[reqSymbolDef] = import;
                return node;
            }
        }

        if (DetectImport(node) is {} import2)
        {
            if (!(Parent() is AstSimpleStatement))
                _sourceFile.NeedsImports.AddUnique(import2);
            return node;
        }

        if (node is AstSimpleStatement {Body: var stmBody })
        {
            if (stmBody is AstCall call)
            {
                if (call.IsDefinePropertyExportsEsModule())
                {
                    return Remove;
                }

                // Object.defineProperty(exports, "ExportName", {
                //    enumerable: true,
                //    get: function() {
                //      return SomeImportDotPath;
                //    }
                // });
                if (call.Expression is AstDot astDot &&
                    astDot.Expression.IsSymbolDef().IsGlobalSymbol() == "Object" &&
                    astDot.PropertyAsString == "defineProperty"
                    && call.Args.Count == 3 && call.Args[0].IsSymbolDef().IsExportsSymbol() &&
                    call.Args[1] is AstString exportName && call.Args[2] is AstObject { Properties.Count: 2 } astObject && astObject.Properties.Last is AstObjectProperty
                    {
                        Value: AstLambda
                        {
                            Body.Last: AstReturn astRet
                        }
                    } && DetectImport(astRet.Value) is {} bindPath)
                {
                    _sourceFile.SelfExports.Add(new ReexportSelfExport(exportName.Value, bindPath.Item1, bindPath.Item2));
                    return Remove;
                }

                if (call.Expression.IsSymbolDef().IsGlobalSymbol() == "__createBinding" && call.Args.Count >= 3 &&
                    call.Args[0].IsSymbolDef().IsExportsSymbol() && call.Args[2] is AstString bindingName &&
                    DetectImport(call.Args[1]) is {} bindModule && bindModule.Item2.Length == 0)
                {
                    if (call.Args.Count == 3)
                    {
                        _sourceFile.SelfExports.Add(new ReexportSelfExport(bindingName.Value, bindModule.Item1, Concat(bindModule.Item2, bindingName.Value)));
                        return Remove;
                    }
                    if (call.Args.Count == 4 && call.Args[3] is AstString asName)
                    {
                        _sourceFile.SelfExports.Add(new ReexportSelfExport(asName.Value, bindModule.Item1, Concat(bindModule.Item2, bindingName.Value)));
                        return Remove;
                    }
                }

                var callSymbol = call.Expression.IsSymbolDef();
                if ((callSymbol == _reexportSymbol || callSymbol.IsTsReexportSymbol()) && call.Args.Count >= 1)
                {
                    req = call.Args[0].IsRequireCall();
                    if (req != null)
                    {
                        var resolvedReq = _resolver.Invoke(_sourceFile.Name, req);
                        _sourceFile.Requires.AddUnique(resolvedReq);
                        _sourceFile.SelfExports.Add(new ExportStarSelfExport(resolvedReq));
                        return Remove;
                    }
                }
            }

            var pea = stmBody.IsExportsAssign();
            if (pea != null)
            {
                if (pea.Value.name == "__esModule")
                {
                    return Remove;
                }

                if (IsExportsAssignVoid0(pea.Value.value))
                {
                    return Remove;
                }

                string newName;
                if (_exportName2VarNameMap.TryGetValue(pea.Value.name, out var varName))
                {
                    // We overwrite function, true export needs to be morphed to var
                    if (varName.Init is AstLambda)
                    {
                        newName = BundlerHelpers.MakeUniqueName("__export_" + pea.Value.name,
                            _sourceFile.Ast.Variables!,
                            _sourceFile.Ast.CalcNonRootSymbolNames(), "");
                        var newVar = new AstVar(stmBody);
                        var astSymbolVar = new AstSymbolVar(stmBody, newName);
                        astSymbolVar.Thedef = new SymbolDef(_sourceFile.Ast, astSymbolVar, null);
                        _sourceFile.Ast.Variables!.Add(newName, astSymbolVar.Thedef);
                        newVar.Definitions.Add(new AstVarDef(astSymbolVar,
                            new AstSymbolRef(astSymbolVar, varName, SymbolUsage.Read)));
                        _exportName2VarNameMap[pea.Value.name] = astSymbolVar.Thedef;
                        _sourceFile.SelfExports.Add(new SimpleSelfExport(pea.Value.name,
                            new AstSymbolRef(_sourceFile.Ast, astSymbolVar.Thedef, SymbolUsage.Unknown)));
                        _bodyPrepend.Add(newVar);
                        varName = astSymbolVar.Thedef;
                    }
                    ((AstAssign)stmBody).Left =
                        new AstSymbolRef(((AstAssign)stmBody).Left, varName, SymbolUsage.Write);
                    return null;
                }

                if (pea.Value.value.IsConstantSymbolRef())
                {
                    // It could be var symbol of required module, than it is namespace export
                    if (_reqSymbolDefMap.TryGetValue(pea.Value.value.IsSymbolDef()!, out var res) && res.Item2.Length==0)
                    {
                        _sourceFile.SelfExports.Add(new ExportAsNamespaceSelfExport(res.Item1, pea.Value.name));
                    }
                    else
                    {
                        _exportName2VarNameMap[pea.Value.name] = pea.Value.value.IsSymbolDef()!;
                        _sourceFile.SelfExports.Add(new SimpleSelfExport(pea.Value.name, (AstSymbol)pea.Value.value!));
                    }
                    return Remove;
                }

                if (pea.Value.value.IsRequireCall() is {} exportAsNamespace)
                {
                    var resolvedName = _resolver.Invoke(_sourceFile.Name, exportAsNamespace);
                    _sourceFile.Requires.AddUnique(resolvedName);
                    _sourceFile.SelfExports.Add(new ExportAsNamespaceSelfExport(resolvedName, pea.Value.name));
                    return Remove;
                }

                newName = BundlerHelpers.MakeUniqueName("__export_" + pea.Value.name, _sourceFile.Ast.Variables!,
                    _sourceFile.Ast.CalcNonRootSymbolNames(), "");
                if (Parent(1) != null)
                {
                    var newVar = new AstVar(stmBody);
                    var astSymbolVar = new AstSymbolVar(stmBody, newName);
                    astSymbolVar.Thedef = new SymbolDef(_sourceFile.Ast, astSymbolVar, null);
                    _sourceFile.Ast.Variables!.Add(newName, astSymbolVar.Thedef);
                    newVar.Definitions.Add(new AstVarDef(astSymbolVar));
                    _exportName2VarNameMap[pea.Value.name] = astSymbolVar.Thedef;
                    _sourceFile.SelfExports.Add(new SimpleSelfExport(pea.Value.name,
                        new AstSymbolRef(_sourceFile.Ast, astSymbolVar.Thedef, SymbolUsage.Unknown)));
                    _sourceFile.Ast.Body.Add(newVar);
                    ((AstAssign)stmBody).Left =
                        new AstSymbolRef(((AstAssign)stmBody).Left, astSymbolVar.Thedef, SymbolUsage.Write);
                    return node;
                }
                else
                {
                    var newVar = new AstVar(stmBody);
                    var astSymbolVar = new AstSymbolVar(stmBody, newName);
                    astSymbolVar.Thedef = new SymbolDef(_sourceFile.Ast, astSymbolVar, null);
                    _sourceFile.Ast.Variables!.Add(newName, astSymbolVar.Thedef);
                    _exportName2VarNameMap[pea.Value.name] = astSymbolVar.Thedef;
                    var trueValue = Transform(pea.Value.value);
                    newVar.Definitions.Add(new AstVarDef(astSymbolVar, trueValue));
                    astSymbolVar.Thedef.Init = trueValue;
                    _sourceFile.SelfExports.Add(new SimpleSelfExport(pea.Value.name,
                        new AstSymbolRef(_sourceFile.Ast, astSymbolVar.Thedef, SymbolUsage.Unknown)));
                    return newVar;
                }
            }
        }

        if (node is AstDefun { ArgNames.Count: 1 } func && func.Name.IsSymbolDef()?.Name == "__export")
        {
            _reexportSymbol = func.Name.IsSymbolDef();
            return Remove;
        }

        if (node is AstVar { Definitions: { Count: 1, Last.Name: AstSymbol { Name: "__exportStar" } exportStarSymbol } })
        {
            _reexportSymbol = exportStarSymbol.IsSymbolDef();
            return Remove;
        }

        if (node is AstVar { Definitions: { Count: 1, Last.Name: AstSymbol { Name: "__createBinding" } } })
        {
            return Remove;
        }

        if (node is AstPropAccess propAccess && propAccess.Expression.IsSymbolDef().IsExportsSymbol() &&
            propAccess.PropertyAsString is { } name)
        {
            if (_exportName2VarNameMap.TryGetValue(name, out var varName))
            {
                return new AstSymbolRef(node, varName, SymbolUsage.Unknown);
            }

            var newName = BundlerHelpers.MakeUniqueName("__export_" + name, _sourceFile.Ast.Variables!,
                _sourceFile.Ast.CalcNonRootSymbolNames(), "");
            var newVar = new AstVar(node);
            var astSymbolVar = new AstSymbolVar(node, newName);
            astSymbolVar.Thedef = new SymbolDef(_sourceFile.Ast, astSymbolVar, null);
            newVar.Definitions.Add(new AstVarDef(astSymbolVar));
            _exportName2VarNameMap[name] = astSymbolVar.Thedef;
            _sourceFile.Ast.Variables!.Add(newName, astSymbolVar.Thedef);
            _sourceFile.SelfExports.Add(new SimpleSelfExport(name, astSymbolVar));
            _bodyPrepend.Add(newVar);
            return new AstSymbolRef(node, astSymbolVar.Thedef, SymbolUsage.Unknown);
        }

        return null;
    }

    static bool IsExportsAssignVoid0(AstNode? node)
    {
        while (true)
        {
            if (node == null) return false;
            var pea = node.IsExportsAssign();
            if (!pea.HasValue) return node is AstUnary unary && unary.Operator == Operator.Void;
            node = pea!.Value.value;
        }
    }

    protected override AstNode? After(AstNode node, bool inList)
    {
        if (node is AstToplevel toplevel)
        {
            toplevel.Body.InsertRange(0, _bodyPrepend.AsReadOnlySpan());
        }

        return null;
    }
}
