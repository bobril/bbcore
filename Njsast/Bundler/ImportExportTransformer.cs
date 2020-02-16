using System;
using System.Collections.Generic;
using Njsast.Ast;

namespace Njsast.Bundler
{
    public class ImportExportTransformer : TreeTransformer
    {
        readonly SourceFile _sourceFile;
        readonly Func<string, string, string> _resolver;
        readonly Dictionary<string, SymbolDef> _exportName2VarNameMap = new Dictionary<string, SymbolDef>();
        StructList<AstNode> _bodyPrepend = new StructList<AstNode>();
        SymbolDef? _reexportSymbol;
        readonly Dictionary<SymbolDef, string> _reqSymbolDefMap = new Dictionary<SymbolDef, string>();

        public ImportExportTransformer(SourceFile sourceFile, Func<string, string, string> resolver)
        {
            _sourceFile = sourceFile;
            _resolver = resolver;
        }

        protected override AstNode? Before(AstNode node, bool inList)
        {
            var req = node.IsRequireCall();
            if (req != null)
            {
                var resolvedName = _resolver.Invoke(_sourceFile.Name, req);
                _sourceFile.Requires.Add(resolvedName);
                if (!(Parent() is AstVarDef) && !(Parent() is AstSimpleStatement))
                {
                    _sourceFile.NeedsWholeImportsFrom.AddUnique(resolvedName);
                }

                return node;
            }

            req = node.IsLazyImportCall();
            if (req != null)
            {
                _sourceFile.LazyRequires.Add(_resolver.Invoke(_sourceFile.Name, req));
                return node;
            }

            if (node is AstVarDef varDef)
            {
                if (varDef.Value.IsRequireCall() is { } reqName)
                {
                    var reqSymbolDef = varDef.Name.IsSymbolDef()!;
                    var resolvedName = _resolver(_sourceFile.Name, reqName);
                    _sourceFile.Requires.Add(resolvedName);
                    _reqSymbolDefMap[reqSymbolDef] = resolvedName;
                    return node;
                }
            }

            if (node is AstSymbolRef symbRef && _reqSymbolDefMap.TryGetValue(symbRef.Thedef!, out var wholeImportFile))
            {
                if (Parent() is AstPropAccess propAccess2 && propAccess2.Expression == symbRef &&
                    propAccess2.PropertyAsString is {} propName)
                {
                    _sourceFile.NeedsImports.AddUnique((wholeImportFile, propName));
                    return node;
                }

                _sourceFile.NeedsWholeImportsFrom.AddUnique(wholeImportFile);
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

                    var callSymbol = call.Expression.IsSymbolDef();
                    if ((callSymbol == _reexportSymbol || callSymbol.IsTsReexportSymbol()) && call.Args.Count == 1)
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

                    var trueValue = pea.Value.value != null ? Transform(pea.Value.value) : null;
                    if (_exportName2VarNameMap.TryGetValue(pea.Value.name, out var varName))
                    {
                        ((AstAssign) stmBody).Left =
                            new AstSymbolRef(((AstAssign) stmBody).Left, varName, SymbolUsage.Write);
                        return node;
                    }

                    if (trueValue.IsConstantSymbolRef())
                    {
                        _exportName2VarNameMap[pea.Value.name] = trueValue.IsSymbolDef()!;
                        _sourceFile.SelfExports.Add(new SimpleSelfExport(pea.Value.name, (AstSymbol) trueValue!));
                        return Remove;
                    }

                    var newName =
                        BundlerHelpers.MakeUniqueName("__export_" + pea.Value.name, _sourceFile.Ast.Variables!,
                            _sourceFile.Ast.Globals!, "");
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
                        ((AstAssign) stmBody).Left =
                            new AstSymbolRef(((AstAssign) stmBody).Left, astSymbolVar.Thedef, SymbolUsage.Write);
                        return node;
                    }
                    else
                    {
                        var newVar = new AstVar(stmBody);
                        var astSymbolVar = new AstSymbolVar(stmBody, newName);
                        astSymbolVar.Thedef = new SymbolDef(_sourceFile.Ast, astSymbolVar, trueValue);
                        _sourceFile.Ast.Variables!.Add(newName, astSymbolVar.Thedef);
                        newVar.Definitions.Add(new AstVarDef(astSymbolVar, trueValue));
                        _exportName2VarNameMap[pea.Value.name] = astSymbolVar.Thedef;
                        _sourceFile.SelfExports.Add(new SimpleSelfExport(pea.Value.name,
                            new AstSymbolRef(_sourceFile.Ast, astSymbolVar.Thedef, SymbolUsage.Unknown)));
                        return newVar;
                    }
                }
            }

            if (node is AstDefun func && func.ArgNames.Count == 1 && func.Name.IsSymbolDef()?.Name == "__export")
            {
                _reexportSymbol = func.Name.IsSymbolDef();
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
                    _sourceFile.Ast.Globals!, "");
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

        protected override AstNode? After(AstNode node, bool inList)
        {
            if (node is AstToplevel toplevel)
            {
                toplevel.Body.InsertRange(0, _bodyPrepend.AsReadOnlySpan());
            }

            return null;
        }
    }
}
