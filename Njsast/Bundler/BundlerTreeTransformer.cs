using System;
using System.Collections.Generic;
using System.Linq;
using Njsast.Ast;

namespace Njsast.Bundler
{
    class BundlerTreeTransformer : TreeTransformer
    {
        readonly Dictionary<string, SourceFile> _cache;
        readonly IBundlerCtx _ctx;
        readonly SourceFile _currentSourceFile;
        readonly Dictionary<string, SymbolDef> _rootVariables;
        readonly HashSet<string> _nonRootSymbolNames;
        readonly Dictionary<string, SplitInfo> _splitMap;
        readonly string _suffix;

        readonly Dictionary<SymbolDef, (SourceFile, string[])> _reqSymbolDefMap =
            new Dictionary<SymbolDef, (SourceFile, string[])>();

        readonly SplitInfo _splitInfo;

        public BundlerTreeTransformer(Dictionary<string, SourceFile> cache, IBundlerCtx ctx,
            SourceFile currentSourceFile, Dictionary<string, SymbolDef> rootVariables,
            HashSet<string> nonRootSymbolNames, string suffix,
            Dictionary<string, SplitInfo> splitMap, SplitInfo splitInfo)
        {
            _cache = cache;
            _ctx = ctx;
            _currentSourceFile = currentSourceFile;
            _rootVariables = rootVariables;
            _nonRootSymbolNames = nonRootSymbolNames;
            _splitMap = splitMap;
            _suffix = "_" + suffix;
            _splitInfo = splitInfo;
        }

        public (SourceFile, string[])? DetectImport(AstNode? node)
        {
            switch (node)
            {
                case AstCall _ when node.IsRequireCall() is { } reqName:
                {
                    var resolvedName = _ctx.ResolveRequire(reqName, _currentSourceFile!.Name);
                    if (!_cache.TryGetValue(resolvedName, out var reqSource))
                        throw new ApplicationException("Cannot find " + resolvedName + " imported from " +
                                                       _currentSourceFile!.Name);
                    return (reqSource, Array.Empty<string>());
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

        protected override AstNode? Before(AstNode node, bool inList)
        {
            if (node is AstLabel)
                return node;

            if (node is AstVarDef varDef && varDef.Name.IsSymbolDef() is {} reqSymbolDef && reqSymbolDef.IsSingleInit && _currentSourceFile.Exports!.Values().All(n => n.IsSymbolDef() != reqSymbolDef))
            {
                if (DetectImport(varDef.Value) is { } import)
                {
                    _reqSymbolDefMap[reqSymbolDef] = import;
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
                var theDef = CheckIfNewlyUsedSymbolIsUnique((AstSymbol)reqSource.Exports![Array.Empty<string>()]);
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

            if (DetectImport(node) is {} import2)
            {
                if (import2.Item1.OnlyWholeExport && import2.Item2.Length == 1 && import2.Item2[0] == "default")
                {
                    var theDef =
                        CheckIfNewlyUsedSymbolIsUnique((AstSymbol)import2.Item1.Exports![new ReadOnlySpan<string>()]);
                    return new AstSymbolRef(node, theDef, SymbolUsage.Read);
                }
                if (import2.Item1.Exports!.TryFindLongestPrefix(import2.Item2, out var matchLen, out var exportNode))
                {
                    if (matchLen == import2.Item2.Length)
                    {
                        if (exportNode is AstSymbol trueSymbol)
                        {
                            var theDef = CheckIfNewlyUsedSymbolIsUnique(trueSymbol);
                            return new AstSymbolRef(node, theDef, SymbolUsage.Read);
                        }

                        return exportNode;
                    }
                }
                if (!import2.Item1.Exports!.IsJustRoot && import2.Item2.Length == 1)
                {
                    // This is not error because it could be just TypeScript interface
                    return new AstSymbolRef("undefined");
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
            var newName = BundlerHelpers.MakeUniqueName(oldName, _rootVariables, _nonRootSymbolNames, _suffix);
            if (newName == oldName)
                return astSymbolDef;
            _rootVariables[newName] = astSymbolDef;
            Helpers.RenameSymbol(astSymbolDef, newName);
            return astSymbolDef;
        }

        protected override AstNode After(AstNode node, bool inList)
        {
            if (node is AstSimpleStatement simple && simple.Body == Remove)
                return Remove;
            if (node is AstVar var && var.Definitions.Count == 0)
                return Remove;
            return node;
        }
    }
}
