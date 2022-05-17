using System;
using System.Collections.Generic;
using System.Linq;
using Njsast.Ast;

namespace Njsast.Bundler;

class BundlerTreeTransformer : TreeTransformer
{
    readonly Dictionary<string, SourceFile> _cache;
    readonly IBundlerCtx _ctx;
    readonly SourceFile _currentSourceFile;
    readonly Dictionary<string, SymbolDef> _rootVariables;
    readonly HashSet<string> _nonRootSymbolNames;
    readonly Dictionary<string, SplitInfo> _splitMap;
    readonly string _suffix;

    readonly Dictionary<SymbolDef, (SourceFile, string[])> _reqSymbolDefMap = new();

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
                if (resolvedName == IBundlerCtx.LeaveAsExternal)
                {
                    return (new SourceFile(reqName), Array.Empty<string>());
                }
                if (!_cache.TryGetValue(resolvedName, out var reqSource))
                    throw new ApplicationException("Cannot find " + resolvedName + " imported from " +
                                                   _currentSourceFile!.Name);
                return (reqSource, Array.Empty<string>());
            }
            case AstSymbolRef symbolRef when _reqSymbolDefMap.TryGetValue(symbolRef.Thedef!, out var res):
                return res;
            case AstPropAccess propAccess when propAccess.PropertyAsString is { } propName &&
                                               DetectImport(propAccess.Expression) is { } leftImport:
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

    static string[] Concat(string left, string[] right)
    {
        var rightLength = right.Length;
        var res = new string[rightLength + 1];
        res[0] = left;
        right.AsSpan().CopyTo(res.AsSpan()[1..]);
        return res;
    }

    protected override AstNode? Before(AstNode node, bool inList)
    {
        if (node is AstLabel)
            return node;

        if (node is AstVarDef varDef && varDef.Name.IsSymbolDef() is { IsSingleInit: true } reqSymbolDef &&
            _currentSourceFile.Exports!.Values().All(n => n.IsSymbolDef() != reqSymbolDef))
        {
            if (DetectImport(varDef.Value) is { } import)
            {
                _reqSymbolDefMap[reqSymbolDef] = import;
                if (import.Item2.Length == 0) return Remove;
                if (!import.Item1.Exports!.TryFindLongestPrefix(import.Item2, out _, out _))
                    return Remove;
            }
        }

        if (node is AstSimpleStatement simpleStatement)
        {
            if (simpleStatement.Body.IsRequireCall() is { })
                return Remove;
        }

        if (node.IsRequireCall() is { } eagerReqName)
        {
            var resolvedName = _ctx.ResolveRequire(eagerReqName, _currentSourceFile!.Name);
            if (!_cache.TryGetValue(resolvedName, out var reqSource))
                throw new ApplicationException("Cannot find " + resolvedName + " imported from " +
                                               _currentSourceFile!.Name);
            reqSource.CreateWholeExport(Array.Empty<string>());
            var theDef = CheckIfNewlyUsedSymbolIsUnique((AstSymbol)reqSource.Exports![Array.Empty<string>()]);
            return new AstSymbolRef(node, theDef, SymbolUsage.Read);
        }

        if (node.IsLazyImportCall() is { } lazyReqName)
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
                call = new(new AstDot(call, "then"));
                var func = new AstFunction();
                func.Body.Add(new AstReturn(result));
                call.Args.Add(func);
                result = call;
            }

            return result;
        }

        if (DetectImport(node) is { } import2)
        {
            var needPath = import2.Item2.AsSpan();
            if (needPath.Length >= 1 && needPath[0] == "default" &&
                (import2.Item1.Exports!.IsJustRoot ||
                 !import2.Item1.Exports!.TryFindLongestPrefix(new[] { "default" }, out _, out _)))
            {
                needPath = needPath.Slice(1);
            }

            if (import2.Item1.ExternalImport)
            {
                ref var symbol = ref _splitInfo.ImportFromExternals[Concat(import2.Item1.Name, import2.Item2)];
                if (symbol != null)
                {
                    return new AstSymbolRef(symbol);
                }
                var name = BundlerHelpers.MakeUniqueName(import2.Item2[^1], _rootVariables, _nonRootSymbolNames,
                    _suffix);
                symbol = new AstSymbolRef(node, name);
                return symbol;
            }

            if (import2.Item1.OnlyWholeExport && needPath.Length == 0)
            {
                var theDef =
                    CheckIfNewlyUsedSymbolIsUnique((AstSymbol)import2.Item1.Exports![new ReadOnlySpan<string>()]);
                return new AstSymbolRef(node, theDef, SymbolUsage.Read);
            }

            if (import2.Item1.Exports!.TryFindLongestPrefix(needPath, out var matchLen, out var exportNode))
            {
                if (matchLen == needPath.Length)
                {
                    if (exportNode is AstSymbol trueSymbol)
                    {
                        // not needed symbol must be already unique: var theDef = CheckIfNewlyUsedSymbolIsUnique(trueSymbol);
                        return new AstSymbolRef(node, ResolveTrueSymbolDef(trueSymbol), SymbolUsage.Read);
                    }

                    return exportNode;
                }
            }

            if (!import2.Item1.Exports!.IsJustRoot && import2.Item2.Length <= 1)
            {
                // This is not error because it could be just TypeScript interface
                return new AstSymbolRef("undefined");
            }
        }

        return null;
    }

    SymbolDef ResolveTrueSymbolDef(AstSymbol astSymbol)
    {
        if (_splitInfo.ImportsFromOtherBundles.TryGetValue(astSymbol, out var importFromOtherBundle))
        {
            astSymbol = importFromOtherBundle.Ref!;
        }

        return astSymbol.Thedef!;
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
        if (node is AstDefinitions { Definitions.Count: 0 })
            return Remove;
        if (node is AstSequence { Expressions: { Count: 2 } expressions } && expressions[0] is AstNumber { Value:0 } && expressions[1] is AstSymbolRef)
        {
            return expressions[1];
        }
        return node;
    }
}
