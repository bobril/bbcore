using System;
using System.Collections.Generic;
using System.Diagnostics;
using Njsast.Ast;

namespace Njsast.Bundler
{
    class BundlerTreeTransformer : TreeTransformer
    {
        readonly Dictionary<string, SourceFile> _cache;
        readonly IBundlerCtx _ctx;
        readonly SourceFile _currentSourceFile;
        readonly Dictionary<string, SymbolDef> _rootVariables;
        readonly Dictionary<string, SymbolDef> _globals;
        readonly Dictionary<string, SplitInfo> _splitMap;
        readonly string _suffix;
        readonly Dictionary<SymbolDef, SourceFile> _reqSymbolDefMap = new Dictionary<SymbolDef, SourceFile>();
        readonly SplitInfo _splitInfo;

        public BundlerTreeTransformer(Dictionary<string, SourceFile> cache, IBundlerCtx ctx,
            SourceFile currentSourceFile, Dictionary<string, SymbolDef> rootVariables, Dictionary<string, SymbolDef> globals, string suffix,
            Dictionary<string, SplitInfo> splitMap, SplitInfo splitInfo)
        {
            _cache = cache;
            _ctx = ctx;
            _currentSourceFile = currentSourceFile;
            _rootVariables = rootVariables;
            _globals = globals;
            _splitMap = splitMap;
            _suffix = "_" + suffix;
            _splitInfo = splitInfo;
        }

        protected override AstNode? Before(AstNode node, bool inList)
        {
            if (node is AstLabel)
                return node;
            if (node is AstVarDef varDef)
            {
                if (varDef.Value.IsRequireCall() is { } reqName)
                {
                    var reqSymbolDef = varDef.Name.IsSymbolDef()!;
                    var resolvedName = _ctx.ResolveRequire(reqName, _currentSourceFile!.Name);
                    if (!_cache.TryGetValue(resolvedName, out var reqSource))
                        throw new ApplicationException("Cannot find " + resolvedName + " imported from " +
                                                       _currentSourceFile!.Name);
                    _reqSymbolDefMap[reqSymbolDef] = reqSource;
                    if (_currentSourceFile!.NeedsWholeImportsFrom.IndexOf(resolvedName) >= 0)
                    {
                        var theDef = CheckIfNewlyUsedSymbolIsUnique(reqSource.WholeExport!);
                        return new AstVarDef(varDef, varDef.Name, new AstSymbolRef(node, theDef, SymbolUsage.Read));
                    }

                    return Remove;
                }
            }

            if (node is AstSimpleStatement simpleStatement)
            {
                if (simpleStatement.Body.IsRequireCall() is {})
                    return Remove;
            }

            if (node.IsRequireCall() is {} eagerReqName)
            {
                var resolvedName = _ctx.ResolveRequire(eagerReqName, _currentSourceFile!.Name);
                if (!_cache.TryGetValue(resolvedName, out var reqSource))
                    throw new ApplicationException("Cannot find " + resolvedName + " imported from " +
                                                   _currentSourceFile!.Name);
                Debug.Assert(_currentSourceFile!.NeedsWholeImportsFrom.IndexOf(resolvedName) >= 0);
                var theDef = CheckIfNewlyUsedSymbolIsUnique(reqSource.WholeExport!);
                return new AstSymbolRef(node, theDef, SymbolUsage.Read);
            }

            if (node.IsLazyImportCall() is {} lazyReqName)
            {
                var resolvedName = _ctx.ResolveRequire(lazyReqName, _currentSourceFile!.Name);
                if (!_cache.TryGetValue(resolvedName, out var reqSource))
                    throw new ApplicationException("Cannot find " + resolvedName + " lazy imported from " +
                                                   _currentSourceFile!.Name);
                var splitInfo = _splitMap[reqSource.PartOfBundle!];
                var propName = splitInfo.ExportsAllUsedFromLazyBundles[resolvedName];
                if (splitInfo.IsMainSplit)
                {
                    var call = new AstCall(new AstSymbolRef("__import"));
                    call.Args.Add(new AstSymbolRef("undefined"));
                    call.Args.Add(new AstString(propName));
                    return call;
                }

                var result = new AstCall(new AstSymbolRef("__import"));
                result.Args.Add(new AstString(splitInfo.ShortName!));
                result.Args.Add(new AstString(propName));
                for (var i = splitInfo.ExpandedSplitsForcedLazy.Count; i-- > 0;)
                {
                    var usedSplit = splitInfo.ExpandedSplitsForcedLazy[i];
                    var call = new AstCall(new AstSymbolRef("__import"));
                    call.Args.Add(new AstString(usedSplit.ShortName!));
                    call.Args.Add(new AstString(usedSplit.PropName!));
                    call = new AstCall(new AstDot(call, "then"));
                    var func = new AstFunction();
                    func.Body.Add(new AstReturn(result));
                    call.Args.Add(func);
                    result = call;
                }

                return result;
            }

            if (node is AstSymbolRef symbolRef && symbolRef.IsSymbolDef() is {} wholeImport)
            {
                if (!_reqSymbolDefMap.TryGetValue(wholeImport, out var sourceFile))
                    return null;
                var theDef = CheckIfNewlyUsedSymbolIsUnique(sourceFile.WholeExport!);
                return new AstSymbolRef(node, theDef, SymbolUsage.Read);
            }

            if (node is AstPropAccess propAccess)
            {
                if (propAccess.Expression.IsSymbolDef() is {} symbolDef)
                {
                    if (!_reqSymbolDefMap.TryGetValue(symbolDef, out var sourceFile))
                        return null;
                    var propName = propAccess.PropertyAsString;
                    if (propName != null)
                    {
                        if (sourceFile.OnlyWholeExport && propName == "default")
                        {
                            var theDef = CheckIfNewlyUsedSymbolIsUnique(sourceFile.WholeExport!);
                            return new AstSymbolRef(node, theDef, SymbolUsage.Read);
                        }
                        if (sourceFile.Exports!.TryGetValue(propName, out var exportedSymbol))
                        {
                            if (exportedSymbol is AstSymbol trueSymbol)
                            {
                                var theDef = CheckIfNewlyUsedSymbolIsUnique(trueSymbol);
                                return new AstSymbolRef(node, theDef, SymbolUsage.Read);
                            }

                            return exportedSymbol;
                        }

                        if (sourceFile.WholeExport != null)
                        {
                            return null;
                        }

                        // This is not error because it could be just TypeScript interface
                        return new AstSymbolRef("undefined");
                    }
                }
            }

            return null;
        }

        SymbolDef CheckIfNewlyUsedSymbolIsUnique(AstSymbol astSymbol)
        {
            if (_splitInfo.ImportsFromOtherBundles.TryGetValue(astSymbol, out var importFromOtherBundle))
            {
                astSymbol = importFromOtherBundle.Ref!;
            }

            var astSymbolDef = astSymbol.Thedef!;
            var oldName = astSymbolDef.Name;
            if (!_rootVariables.TryGetValue(oldName, out var rootSymbol))
                return astSymbolDef;
            var newName = BundlerHelpers.MakeUniqueName(oldName, _rootVariables, _globals, _suffix);
            _rootVariables[oldName] = astSymbolDef;
            _rootVariables[newName] = rootSymbol;
            Helpers.RenameSymbol(rootSymbol, newName);
            return astSymbolDef;
        }

        protected override AstNode After(AstNode node, bool inList)
        {
            if (node is AstSimpleStatement simple && simple.Body == Remove)
                return Remove;
            if (node is AstVar @var && @var.Definitions.Count == 0)
                return Remove;
            return node;
        }
    }
}
