using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Njsast.Ast;
using Njsast.Compress;
using Njsast.Output;
using Njsast.Reader;
using Njsast.Scope;
using Njsast.SourceMap;

namespace Njsast.Bundler
{
    public interface IBundlerCtx
    {
        (string?, SourceMap.SourceMap?) ReadContent(string fileName);
        IEnumerable<string> GetPlainJsDependencies(string fileName);
        string GenerateBundleName(string forName);
        string ResolveRequire(string name, string from);
        string JsHeaders(string forSplit, bool withImport);
        void WriteBundle(string name, string content);
        void WriteBundle(string name, SourceMapBuilder content);
        void ReportTime(string name, TimeSpan duration);
        void ModifyBundle(string name, AstToplevel topLevelAst);
    }

    public class BundlerImpl
    {
        public BundlerImpl(IBundlerCtx ctx)
        {
            _ctx = ctx;
            PartToMainFilesMap = new Dictionary<string, IReadOnlyList<string>>();
            MangleWithFrequencyCounting = true;
            GlobalDefines = new Dictionary<string, object>();
        }

        public IReadOnlyDictionary<string, IReadOnlyList<string>> PartToMainFilesMap;
        public ICompressOptions? CompressOptions;
        public bool Mangle;
        public bool MangleWithFrequencyCounting;
        public bool GenerateSourceMap;
        public OutputOptions? OutputOptions;
        public IReadOnlyDictionary<string, object> GlobalDefines;
        readonly IBundlerCtx _ctx;

        StructList<SourceFile> _order = new StructList<SourceFile>();
        readonly Dictionary<string, SourceFile> _cache = new Dictionary<string, SourceFile>();
        readonly Dictionary<string, SplitInfo> _splitMap = new Dictionary<string, SplitInfo>();
        SourceFile? _currentSourceFile;
        string? _currentFileIdent;

        public void Run()
        {
            var stopwatch = Stopwatch.StartNew();
            foreach (var (splitName, mainFileList) in PartToMainFilesMap)
            {
                foreach (var mainFile in mainFileList)
                {
                    if (_cache.TryGetValue(mainFile, out var sourceFile))
                    {
                        MarkRequiredAs(sourceFile, splitName);
                        continue;
                    }

                    Check(mainFile, splitName);
                }

                _splitMap[splitName] = new SplitInfo(splitName)
                    {ShortName = _ctx.GenerateBundleName(splitName), PropName = "ERROR", IsMainSplit = true};
            }

            stopwatch.Stop();
            _ctx.ReportTime("Parse", stopwatch.Elapsed);
            foreach (var (_, sourceFile) in _cache)
            {
                if (sourceFile.NeedsWholeExport && sourceFile.WholeExport == null)
                {
                    sourceFile.CreateWholeExport();
                }
            }

            var lazySplitCounter = 0;
            foreach (var f in _order)
            {
                var fullBundleName = f.PartOfBundle!;
                if (!_splitMap.TryGetValue(fullBundleName, out var split))
                {
                    var shortenedBundleName = _ctx.GenerateBundleName(fullBundleName);
                    split = new SplitInfo(fullBundleName)
                    {
                        ShortName = shortenedBundleName,
                        PropName = BundlerHelpers.NumberToIdent(lazySplitCounter++)
                    };
                    _splitMap[fullBundleName] = split;
                }

                foreach (var dependency in f.PlainJsDependencies)
                {
                    split.PlainJsDependencies.Add(dependency);
                }
            }

            if (lazySplitCounter > 0)
            {
                DetectBundleExportsImports(ref lazySplitCounter);
            }

            foreach (var (splitName, splitInfo) in _splitMap)
            {
                var topLevelAst = Parser.Parse(_ctx.JsHeaders(splitName, lazySplitCounter > 0));
                if (GlobalDefines != null && GlobalDefines.Count > 0)
                    topLevelAst.Body.Add(Helpers.EmitVarDefines(GlobalDefines));
                topLevelAst.FigureOutScope();
                foreach (var jsDependency in splitInfo.PlainJsDependencies)
                {
                    var content = _ctx.ReadContent(jsDependency);
                    var jsAst = Parser.Parse(content.Item1!);
                    content.Item2?.ResolveInAst(jsAst);
                    jsAst.FigureOutScope();
                    _currentFileIdent = BundlerHelpers.FileNameToIdent(jsDependency);
                    BundlerHelpers.AppendToplevelWithRename(topLevelAst, jsAst, _currentFileIdent);
                }

                foreach (var sourceFile in _order)
                {
                    if (sourceFile.PartOfBundle != splitName) continue;
                    foreach (var keyValuePair in sourceFile.Ast.Globals!)
                    {
                        topLevelAst.Globals!.TryAdd(keyValuePair.Key, keyValuePair.Value);
                    }
                }

                AddExternallyImportedFromOtherBundles(topLevelAst, splitInfo);
                foreach (var sourceFile in _order)
                {
                    if (sourceFile.PartOfBundle != splitName) continue;
                    _currentSourceFile = sourceFile;
                    _currentFileIdent = BundlerHelpers.FileNameToIdent(sourceFile.Name);
                    BundlerHelpers.AppendToplevelWithRename(topLevelAst, sourceFile.Ast, _currentFileIdent
                        , BeforeAdd);
                }

                AddExportsFromLazyBundle(splitInfo, topLevelAst);

                BundlerHelpers.WrapByIIFE(topLevelAst);
                if (lazySplitCounter > 0 && PartToMainFilesMap.ContainsKey(splitName))
                {
                    var astVar = new AstVar(topLevelAst);
                    astVar.Definitions.Add(new AstVarDef(new AstSymbolVar("__bbb"), new AstObject()));
                    topLevelAst.Body.Insert(0) = astVar;
                }

                if (CompressOptions != null)
                {
                    stopwatch = Stopwatch.StartNew();
                    topLevelAst.FigureOutScope();
                    topLevelAst = topLevelAst.Compress(CompressOptions, new ScopeOptions
                    {
                        TopLevel = false,
                        BeforeMangling = IgnoreEvalInTwoScopes
                    });
                    stopwatch.Stop();
                    _ctx.ReportTime("Compress", stopwatch.Elapsed);
                }

                if (Mangle)
                {
                    stopwatch = Stopwatch.StartNew();
                    topLevelAst.Mangle(new ScopeOptions
                    {
                        FrequencyCounting = MangleWithFrequencyCounting, TopLevel = false,
                        BeforeMangling = IgnoreEvalInTwoScopes
                    }, OutputOptions);
                    stopwatch.Stop();
                    _ctx.ReportTime("Mangle", stopwatch.Elapsed);
                }

                _ctx.ModifyBundle(splitInfo.ShortName!, topLevelAst);

                if (GenerateSourceMap)
                {
                    stopwatch = Stopwatch.StartNew();
                    var builder = new SourceMapBuilder();
                    topLevelAst.PrintToBuilder(builder, OutputOptions);
                    stopwatch.Stop();
                    _ctx.ReportTime("Print", stopwatch.Elapsed);
                    _ctx.WriteBundle(splitInfo.ShortName!, builder);
                }
                else
                {
                    stopwatch = Stopwatch.StartNew();
                    var content = topLevelAst.PrintToString(OutputOptions);
                    stopwatch.Stop();
                    _ctx.ReportTime("Print", stopwatch.Elapsed);
                    _ctx.WriteBundle(splitInfo.ShortName!, content);
                }
            }
        }

        void DetectBundleExportsImports(ref int lazySplitCounter)
        {
            foreach (var f in _order)
            {
                var sourceSplit = _splitMap[f.PartOfBundle!];
                foreach (var lazyRequire in f.LazyRequires)
                {
                    var targetFile = _cache[lazyRequire];
                    if (targetFile.WholeExport == null)
                    {
                        targetFile.CreateWholeExport();
                    }

                    var targetSplit = _splitMap[targetFile.PartOfBundle!];
                    if (targetSplit.ExportsAllUsedFromLazyBundles.ContainsKey(lazyRequire)) continue;
                    if (targetSplit.FullName == lazyRequire)
                    {
                        targetSplit.ExportsAllUsedFromLazyBundles[lazyRequire] = targetSplit.PropName!;
                    }
                    else if (PartToMainFilesMap.ContainsKey(targetSplit.FullName))
                    {
                        targetSplit.ExportsAllUsedFromLazyBundles[lazyRequire] =
                            BundlerHelpers.NumberToIdent(lazySplitCounter++);
                    }
                }

                foreach (var require in f.Requires)
                {
                    var targetSplit = _splitMap[_cache[require].PartOfBundle!];
                    if (targetSplit != sourceSplit && !PartToMainFilesMap.ContainsKey(targetSplit.FullName))
                    {
                        sourceSplit.DirectSplitsForcedLazy.Add(targetSplit);
                    }
                }

                foreach (var (fromName, exportName) in f.NeedsImports)
                {
                    var fromFile = _cache[fromName];
                    var fromSplit = _splitMap[fromFile.PartOfBundle!];
                    if (sourceSplit == fromSplit)
                        continue;
                    var astNode = fromFile.Exports![exportName];
                    sourceSplit.ImportsFromOtherBundles[astNode] =
                        new ImportFromOtherBundle(fromSplit, fromFile, exportName);
                    fromSplit.ExportsUsedFromLazyBundles[astNode] = BundlerHelpers.NumberToIdent(lazySplitCounter++);
                }

                foreach (var fromName in f.NeedsWholeImportsFrom)
                {
                    var fromFile = _cache[fromName];
                    var fromSplit = _splitMap[fromFile.PartOfBundle!];
                    if (sourceSplit == fromSplit)
                        continue;
                    fromFile.CreateWholeExport();
                    var astNode = fromFile.WholeExport!;
                    sourceSplit.ImportsFromOtherBundles[astNode] =
                        new ImportFromOtherBundle(fromSplit, fromFile, null);
                    fromSplit.ExportsUsedFromLazyBundles[astNode] = BundlerHelpers.NumberToIdent(lazySplitCounter++);
                }

            }

            foreach (var (_, split) in _splitMap)
            {
                if (PartToMainFilesMap.ContainsKey(split.FullName)) continue;
                ExpandDirectSplitsForcedLazy(split, split, new HashSet<SplitInfo> {split});
            }
        }

        static void ExpandDirectSplitsForcedLazy(SplitInfo split, SplitInfo rootSplit, ISet<SplitInfo> visited)
        {
            foreach (var targetSplit in split.DirectSplitsForcedLazy)
            {
                if (!visited.Add(targetSplit)) continue;
                ExpandDirectSplitsForcedLazy(targetSplit, rootSplit, visited);
            }

            if (split != rootSplit) rootSplit.ExpandedSplitsForcedLazy.Add(split);
        }

        void AddExportsFromLazyBundle(SplitInfo splitInfo, AstToplevel topLevelAst)
        {
            foreach (var (fromSourceName, propName) in splitInfo.ExportsAllUsedFromLazyBundles)
            {
                var fromSourceFile = _cache[fromSourceName];
                topLevelAst.Body.Add(new AstSimpleStatement(
                    new AstAssign(new AstDot(new AstSymbolRef("__bbb"), propName), fromSourceFile.WholeExport!)));
            }

            foreach (var (node, propName) in splitInfo.ExportsUsedFromLazyBundles)
            {
                topLevelAst.Body.Add(new AstSimpleStatement(
                    new AstAssign(new AstDot(new AstSymbolRef("__bbb"), propName), node)));
            }
        }

        void AddExternallyImportedFromOtherBundles(AstToplevel toplevel, SplitInfo split)
        {
            foreach (var (astNode, importFromOtherBundle) in split.ImportsFromOtherBundles)
            {
                var name = "__" + importFromOtherBundle.Name + "_" + BundlerHelpers.FileNameToIdent(importFromOtherBundle.FromFile.Name);
                name = BundlerHelpers.MakeUniqueName(name, toplevel.Variables!, toplevel.Globals!, null);
                var shortenedPropertyName = importFromOtherBundle.FromSplit.ExportsUsedFromLazyBundles[astNode];
                var newVar = new AstVar(toplevel);
                var astSymbolVar = new AstSymbolVar(toplevel, name);
                var trueValue = new AstDot(new AstSymbolRef("__bbb"), shortenedPropertyName);
                astSymbolVar.Thedef = new SymbolDef(toplevel, astSymbolVar, trueValue);
                toplevel.Variables!.Add(name, astSymbolVar.Thedef);
                newVar.Definitions.Add(new AstVarDef(astSymbolVar, trueValue));
                toplevel.Body.Add(newVar);
                importFromOtherBundle.Ref = new AstSymbolRef(toplevel, astSymbolVar.Thedef, SymbolUsage.Read);
            }
        }

        void IgnoreEvalInTwoScopes(AstNode astNode)
        {
            if (astNode is AstToplevel topLevel)
                if (topLevel.UsesEval || topLevel.UsesWith)
                {
                    new EvalIgnoreWalker().Walk(topLevel);
                }
        }

        class EvalIgnoreWalker : TreeWalker
        {
            protected override void Visit(AstNode node)
            {
                switch (node)
                {
                    case AstToplevel top:
                        top.UsesEval = false;
                        top.UsesWith = false;
                        DescendOnce();
                        break;
                    case AstScope scope:
                        scope.UsesEval = false;
                        scope.UsesWith = false;
                        StopDescending();
                        break;
                }
            }
        }

        void BeforeAdd(AstToplevel top)
        {
            var transformer =
                new BundlerTreeTransformer(_cache, _ctx, _currentSourceFile!, top.Variables!, top.Globals!, _currentFileIdent!,
                    _splitMap, _splitMap[_currentSourceFile!.PartOfBundle!]);
            transformer.Transform(top);
        }

        void MarkRequiredAs(SourceFile sourceFile, string fromSplitName)
        {
            if (sourceFile.PartOfBundle != fromSplitName && !PartToMainFilesMap.ContainsKey(sourceFile.PartOfBundle!))
            {
                if (PartToMainFilesMap.ContainsKey(fromSplitName))
                {
                    sourceFile.PartOfBundle = fromSplitName;
                }
                else
                {
                    // imported from 2 lazy bundles => promote to independent lazy bundle
                    PromoteToIndependentLazyBundle(sourceFile, sourceFile.Name);
                }
            }
        }

        void PromoteToIndependentLazyBundle(SourceFile sourceFile, string splitName)
        {
            if (sourceFile.PartOfBundle == splitName) return;
            if (PartToMainFilesMap.ContainsKey(sourceFile.PartOfBundle!)) return;
            sourceFile.PartOfBundle = splitName;
            foreach (var fileRequire in sourceFile.Requires)
            {
                PromoteToIndependentLazyBundle(_cache[fileRequire], splitName);
            }
        }

        void Check(string fileName, string splitName)
        {
            var (content, sourceMap) = _ctx.ReadContent(fileName);
            if (content is null) throw new ApplicationException($"Content for {fileName} not found");
            var cached = BundlerHelpers.BuildSourceFile(fileName, content, sourceMap,
                (from, name) => _ctx.ResolveRequire(name, from));
            cached.PartOfBundle = splitName;
            cached.PlainJsDependencies.AddRange(_ctx.GetPlainJsDependencies(fileName).ToArray());
            _cache[fileName] = cached;
            foreach (var r in cached.Requires)
            {
                if (_cache.TryGetValue(r, out var rCached))
                {
                    MarkRequiredAs(rCached, cached.PartOfBundle);
                    continue;
                }

                Check(r, cached.PartOfBundle);
            }

            foreach (var r in cached.LazyRequires)
            {
                if (_cache.TryGetValue(r, out var rCached))
                {
                    MarkRequiredAs(rCached, r);
                    continue;
                }

                Check(r, r);
            }

            foreach (var r in cached.NeedsWholeImportsFrom)
            {
                _cache[r].NeedsWholeExport = true;
            }

            cached.Exports = new Dictionary<string, AstNode>();
            foreach (var exp in cached.SelfExports)
            {
                if (exp is SimpleSelfExport simpleExp)
                {
                    cached.Exports![simpleExp.Name] = simpleExp.Symbol;
                }
                else if (exp is ExportStarSelfExport starExp)
                {
                    var reexModule = _cache[starExp.SourceName];
                    if (reexModule.Exports != null)
                    {
                        foreach (var reexModuleExport in reexModule.Exports)
                        {
                            cached.Exports[reexModuleExport.Key] = reexModuleExport.Value;
                        }
                    }
                    else
                    {
                        foreach (var reexModuleSelfExport in reexModule.SelfExports)
                        {
                            if (reexModuleSelfExport is SimpleSelfExport simpleExp2)
                            {
                                cached.Exports![simpleExp2.Name] = simpleExp2.Symbol;
                            }
                        }
                    }
                }
            }

            _order.Add(cached);
        }
    }
}
