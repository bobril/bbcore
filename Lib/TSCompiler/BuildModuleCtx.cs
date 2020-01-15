using Lib.DiskCache;
using Lib.Utils;
using System;
using System.Linq;
using Lib.Composition;
using System.Text.RegularExpressions;
using Njsast.SourceMap;
using Njsast;
using Njsast.Reader;
using Njsast.ConstEval;
using Njsast.Bobril;
using Njsast.Ast;
using System.Collections.Generic;
using Lib.BuildCache;

namespace Lib.TSCompiler
{
    public class BuildModuleCtx : IImportResolver
    {
        public BuildCtx BuildCtx;
        public TSProject Owner;
        public BuildResult Result;
        public MainBuildResult MainResult;
        public int IterationId;
        TSFileAdditionalInfo _currentlyTranspiling;

        static readonly string[] ExtensionsToImport = {".tsx", ".ts", ".d.ts", ".jsx", ".js", ""};
        static readonly string[] ExtensionsToImportFromJs = {".jsx", ".js", ""};

        internal OrderedHashSet<string> ToCheck;
        internal uint CrawledCount;

        static bool IsDts(ReadOnlySpan<char> name)
        {
            return name.EndsWith(".d.ts");
        }

        static bool IsTsOrTsx(ReadOnlySpan<char> name)
        {
            return name.EndsWith(".ts") || name.EndsWith(".tsx");
        }

        static bool IsTsOrTsxOrJsOrJsx(ReadOnlySpan<char> name)
        {
            return name.EndsWith(".ts") || name.EndsWith(".tsx") || name.EndsWith(".js") || name.EndsWith(".jsx");
        }

        public TSProject ResolveModule(string name)
        {
            if (Result.Modules.TryGetValue(name, out var module))
            {
                if (module.IterationId == IterationId)
                    return module.Valid ? module : null;
                for (uint i = 0; i < module.NegativeChecks.Count; i++)
                {
                    if (CheckItemExistence(module.NegativeChecks[i])) goto again;
                }

                if (module.Valid)
                {
                    module.LoadProjectJson(true, Owner.ProjectOptions);
                    if (module.PackageJsonChangeId == -1) goto again;
                }

                module.IterationId = IterationId;
                return module.Valid ? module : null;
            }

            again: ;
            var negativeChecks = new BTDB.Collections.StructList<string>();
            var dir = Owner.Owner.FullPath;
            while (dir.Length > 0)
            {
                var dc = Owner.DiskCache.TryGetItem(dir + "/node_modules/" + name) as IDirectoryCache;
                if (dc == null || dc.IsInvalid)
                {
                    negativeChecks.Add(dir + "/node_modules/" + name);
                }
                else
                {
                    if (dc.FullPath != dir + "/node_modules/" + name)
                    {
                        // Create it with proper casing
                        return ResolveModule(dc.Name);
                    }

                    module = TSProject.Create(dc, Owner.DiskCache, Owner.Logger, dc.Name);
                    module.LoadProjectJson(true, Owner.ProjectOptions);
                    if (module.PackageJsonChangeId != -1)
                    {
                        module.NegativeChecks.AddRange(negativeChecks.AsSpan());
                        module.IterationId = IterationId;
                        Result.Modules[name] = module;
                        return module;
                    }
                }

                dir = PathUtils.Parent(dir).ToString();
            }

            module = TSProject.CreateInvalid(name);
            module.NegativeChecks.TransferFrom(ref negativeChecks);
            module.IterationId = IterationId;
            Result.Modules[name] = module;
            return null;
        }

        public bool CheckFileExistence(string name)
        {
            var f = Owner.DiskCache.TryGetItem(name) as IFileCache;
            return f != null && !f.IsInvalid;
        }

        public bool CheckItemExistence(string name)
        {
            var f = Owner.DiskCache.TryGetItem(name);
            return f != null && !f.IsInvalid;
        }

        public void ReportMissingImport(string from, string name)
        {
            if (Result.Path2FileInfo.TryGetValue(from, out var parentInfo))
            {
                parentInfo.ReportDiag(true, -15, "Cannot resolve import '" + name + "'", 0, 0, 0, 0);
            }
        }

        // returns "?" if error in resolving
        public string ResolveImport(string from, string name, bool preferDts = false, bool isAsset = false)
        {
            if (Result.ResolveCache.TryGetValue((from, name), out var res))
            {
                if (res.IterationId == IterationId) return res.FileName;
                if (res.FileName != null && !CheckFileExistence(res.FileName)) goto again;
                for (uint i = 0; i < res.NegativeChecks.Count; i++)
                {
                    if (CheckItemExistence(res.NegativeChecks[i])) goto again;
                }
            }

            again: ;
            if (res == null)
            {
                res = new ResolveResult();
                Result.ResolveCache.Add((from, name), res);
            }
            else
            {
                res.FileName = null;
                res.NegativeChecks.Clear();
            }

            res.IterationId = IterationId;
            var relative = name.StartsWith("./") || name.StartsWith("../");
            Result.Path2FileInfo.TryGetValue(from, out var parentInfo);
            string fn = null;
            if (relative)
            {
                fn = PathUtils.Join(parentInfo.Owner.Parent.FullPath, name);
                var browserResolve = parentInfo.FromModule?.ProjectOptions?.BrowserResolve;
                if (browserResolve != null)
                {
                    var relativeToModule = PathUtils.Subtract(fn+".js", parentInfo.FromModule.Owner.FullPath);
                    if (!relativeToModule.StartsWith("../")) relativeToModule = "./" + relativeToModule;
                    if (browserResolve.TryGetValue(relativeToModule, out var resolveReplace))
                    {
                        fn = PathUtils.Join(parentInfo.FromModule.Owner.FullPath, resolveReplace);
                    }
                }
            }

            relative: ;
            if (relative)
            {
                if (fn.EndsWith(".json") || fn.EndsWith(".css"))
                {
                    var fc = Owner.DiskCache.TryGetItem(fn) as IFileCache;
                    if (fc != null && !fc.IsInvalid)
                    {
                        res.FileName = fn;
                        CheckAdd(fn,
                            fn.EndsWith(".json")
                                ? FileCompilationType.Json
                                : (isAsset ? FileCompilationType.Css : FileCompilationType.ImportedCss));
                        return res.FileName;
                    }

                    res.NegativeChecks.Add(fn);
                }

                var dirPath = PathUtils.Parent(fn).ToString();
                var fileOnly = fn.Substring(dirPath.Length + 1);
                IFileCache item = null;
                var dc = Owner.DiskCache.TryGetItem(dirPath) as IDirectoryCache;
                if (dc == null || dc.IsInvalid)
                {
                    res.FileName = "?";
                    res.NegativeChecks.Add(dirPath.ToString());
                    return res.FileName;
                }

                item = (parentInfo.Type == FileCompilationType.EsmJavaScript
                        ? ExtensionsToImportFromJs
                        : ExtensionsToImport).Select(ext =>
                    {
                        var ff = dc.TryGetChild(fileOnly + ext) as IFileCache;
                        if (ff == null || ff.IsInvalid)
                        {
                            res.NegativeChecks.Add(dirPath + "/" + fileOnly + ext);
                            return null;
                        }

                        return ff;
                    })
                    .FirstOrDefault(i => i != null);
                if (item == null)
                {
                    res.FileName = "?";
                    return res.FileName;
                }

                res.FileName = item.FullPath;
                if (item.FullPath.Substring(0, fn.Length) != fn)
                {
                    parentInfo.ReportDiag(false, -1,
                        "Local import has wrong casing '" + fn + "' on disk '" + item.FullPath + "'", 0, 0, 0, 0);
                }

                if (IsDts(item.Name))
                {
                    CheckAdd(item.FullPath, FileCompilationType.TypeScriptDefinition);
                    if (dc.TryGetChild(fileOnly + ".js") is IFileCache jsItem)
                    {
                        CheckAdd(jsItem.FullPath, FileCompilationType.EsmJavaScript);
                        res.FileNameJs = jsItem.FullPath;
                        parentInfo.ReportDependency(jsItem.FullPath);
                    }
                    else
                    {
                        res.NegativeChecks.Add(dirPath + "/" + fileOnly + ".js");
                        // implementation for .d.ts file does not have same name, it needs to be added to build by b.asset("lib.js") and cannot have dependencies
                    }
                }
                else if (IsTsOrTsxOrJsOrJsx(item.Name))
                {
                    CheckAdd(item.FullPath,
                        IsTsOrTsx(item.Name) ? FileCompilationType.TypeScript :
                        isAsset ? FileCompilationType.JavaScriptAsset : FileCompilationType.EsmJavaScript);
                }
                else
                {
                    CheckAdd(item.FullPath, FileCompilationType.Unknown);
                }

                return res.FileNameWithPreference(preferDts);
            }
            else
            {
                var pos = 0;
                PathUtils.EnumParts(name, ref pos, out var mn, out _);
                string mname;
                if (name[0] == '@')
                {
                    PathUtils.EnumParts(name, ref pos, out var mn2, out _);
                    mname = mn.ToString() + "/" + mn2.ToString();
                }
                else
                {
                    mname = mn.ToString();
                }

                string? mainFileReplace = null;
                if (mname.Length == name.Length && parentInfo != null)
                {
                    var browserResolve = parentInfo.FromModule?.ProjectOptions?.BrowserResolve;
                    if (browserResolve != null)
                    {
                        if (browserResolve.TryGetValue(name, out var resolveReplace))
                        {
                            if (resolveReplace == null)
                            {
                                res.FileName = "<empty>";
                                return res.FileName;
                            }
                            if (!resolveReplace.StartsWith(name + "/"))
                            {
                                fn = PathUtils.Join(parentInfo.FromModule.Owner.FullPath, resolveReplace);
                                relative = true;
                                goto relative;
                            }

                            mainFileReplace = resolveReplace.Substring(name.Length + 1);
                        }
                    }
                }

                var moduleInfo = ResolveModule(mname);
                if (moduleInfo == null)
                {
                    ReportMissingImport(from, name);
                    res.FileName = "?";
                    return res.FileName;
                }

                if (PathUtils.GetFile(mname) != moduleInfo.Name)
                {
                    parentInfo.ReportDiag(false, -2,
                        "Module import has wrong casing '" + mname + "' on disk '" + moduleInfo.Name + "'", 0, 0, 0, 0);
                }

                if (mname.Length != name.Length)
                {
                    fn = PathUtils.Join(moduleInfo.Owner.FullPath, name.Substring(mname.Length + 1));
                    relative = true;
                    goto relative;
                }

                if (mainFileReplace != null)
                {
                    moduleInfo.MainFile = mainFileReplace;
                }

                var mainFile = PathUtils.Join(moduleInfo.Owner.FullPath, moduleInfo.MainFile);
                res.FileName = mainFile;
                CheckAdd(mainFile,
                    IsTsOrTsx(mainFile) ? FileCompilationType.TypeScript : FileCompilationType.EsmJavaScript);

                if (moduleInfo.ProjectOptions?.ObsoleteMessage != null)
                {
                    if (!PragmaParser.ParseIgnoreImportingObsolete(parentInfo.Owner.Utf8Content).Contains(name))
                    {
                        parentInfo.ReportDiag(false, -14,
                            "Importing obsolete module: " + moduleInfo.ProjectOptions?.ObsoleteMessage, 0, 0, 0, 0);
                    }
                }

                return res.FileName;
            }
        }

        bool TryToResolveFromBuildCache(TSFileAdditionalInfo itemInfo)
        {
            itemInfo.TakenFromBuildCache = false;
            var bc = Owner.ProjectOptions.BuildCache;
            if (bc.IsEnabled)
            {
                byte[] hashOfContent;
                try
                {
                    hashOfContent = itemInfo.Owner.HashOfContent;
                }
                catch
                {
                    // File was probably renamed or deleted
                    return false;
                }

                var confId = Owner.ProjectOptions.ConfigurationBuildCacheId;
                var fbc = bc.FindTSFileBuildCache(hashOfContent, confId);
                if (fbc != null)
                {
                    if (MatchingTranspilationDendencies(itemInfo.Owner, fbc.TranspilationDependencies))
                    {
                        if (MakeSourceInfoAbsolute(fbc.SourceInfo, itemInfo.Owner.FullPath))
                        {
                            itemInfo.Output = fbc.Output;
                            itemInfo.MapLink = fbc.MapLink;
                            if (itemInfo.MapLink?.sources?.Count == 1)
                            {
                                itemInfo.MapLink.sources[0] = itemInfo.Owner.FullPath;
                            }

                            itemInfo.SourceInfo = fbc.SourceInfo;
                            itemInfo.TranspilationDependencies = fbc.TranspilationDependencies;
                            itemInfo.TakenFromBuildCache = true;
                            AddDependenciesFromSourceInfo(itemInfo);
                            //_owner.Logger.Info("Loaded from cache " + itemInfo.Owner.FullPath);
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        bool MatchingTranspilationDendencies(IFileCache owner, List<DependencyTriplet> transpilationDependencies)
        {
            if (transpilationDependencies == null) return true;
            var hashToName = new Dictionary<byte[], string>(StructuralEqualityComparer<byte[]>.Default);
            hashToName.Add(owner.HashOfContent, owner.FullPath);
            var processed = new StructList<bool>();
            processed.RepeatAdd(false, (uint) transpilationDependencies.Count);
            bool somethingFailed;
            do
            {
                var somethingProcessed = false;
                somethingFailed = false;
                for (var i = 0u; i < processed.Count; i++)
                {
                    if (processed[i]) continue;
                    var dep = transpilationDependencies[(int) i];
                    if (!hashToName.TryGetValue(dep.SourceHash, out var sourceName))
                    {
                        somethingFailed = true;
                        continue;
                    }

                    somethingProcessed = true;
                    processed[i] = true;
                    var targetName = ResolveImport(sourceName, dep.Import);
                    if (targetName == "?")
                    {
                        return false;
                    }

                    Result.Path2FileInfo.TryGetValue(targetName, out var targetInfo);
                    if (!dep.TargetHash.AsSpan().SequenceEqual(targetInfo.Owner.HashOfContent))
                    {
                        return false;
                    }

                    hashToName.TryAdd(targetInfo.Owner.HashOfContent, targetInfo.Owner.FullPath);
                }

                if (!somethingProcessed) return !somethingFailed;
            } while (somethingFailed);

            return true;
        }

        bool TryToResolveFromBuildCacheCss(TSFileAdditionalInfo itemInfo)
        {
            itemInfo.TakenFromBuildCache = false;
            var bc = Owner.ProjectOptions.BuildCache;
            if (bc.IsEnabled)
            {
                var hashOfContent = itemInfo.Owner.HashOfContent;
                var fbc = bc.FindTSFileBuildCache(hashOfContent, 0);
                if (fbc != null)
                {
                    itemInfo.Output = fbc.Output;
                    itemInfo.TranspilationDependencies = fbc.TranspilationDependencies;
                    itemInfo.TakenFromBuildCache = true;
                    //_owner.Logger.Info("Loaded from cache " + itemInfo.Owner.FullPath);
                    return true;
                }
            }

            return false;
        }

        public void StoreResultToBuildCache(BuildResult result)
        {
            var bc = Owner.ProjectOptions.BuildCache;
            foreach (var f in result.RecompiledIncrementaly)
            {
                if (f.TakenFromBuildCache)
                    continue;
                if (f.Diagnostics.Count != 0)
                    continue;
                switch (f.Type)
                {
                    case FileCompilationType.TypeScript:
                    case FileCompilationType.EsmJavaScript:
                    {
                        var fbc = new TSFileBuildCache();
                        fbc.ConfigurationId = Owner.ProjectOptions.ConfigurationBuildCacheId;
                        fbc.ContentHash = f.Owner.HashOfContent;
                        fbc.Output = f.Output;
                        fbc.MapLink = f.MapLink;
                        MakeSourceInfoRelative(f.SourceInfo, f.Owner.Parent.FullPath);
                        fbc.SourceInfo = f.SourceInfo;
                        fbc.TranspilationDependencies = f.TranspilationDependencies;
                        bc.Store(fbc);
                        f.TakenFromBuildCache = true;
                        //_owner.Logger.Info("Storing to cache " + f.Owner.FullPath);
                        break;
                    }
                    case FileCompilationType.Css:
                    case FileCompilationType.ImportedCss:
                    {
                        var fbc = new TSFileBuildCache();
                        fbc.ConfigurationId = 0;
                        fbc.ContentHash = f.Owner.HashOfContent;
                        fbc.Output = f.Output;
                        fbc.TranspilationDependencies = f.TranspilationDependencies;
                        bc.Store(fbc);
                        f.TakenFromBuildCache = true;
                        //_owner.Logger.Info("Storing to cache " + f.Owner.FullPath);
                        break;
                    }
                }
            }
        }

        public TSFileAdditionalInfo CheckAdd(string fullNameWithExtension, FileCompilationType compilationType)
        {
            if (!Result.Path2FileInfo.TryGetValue(fullNameWithExtension, out var info))
            {
                var fc = Owner.DiskCache.TryGetItem(fullNameWithExtension) as IFileCache;
                if (fc == null || fc.IsInvalid) return null;
                info = TSFileAdditionalInfo.Create(fc, Owner.DiskCache);
                info.Type = compilationType;
                MainResult.MergeCommonSourceDirectory(fc.FullPath);
                Result.Path2FileInfo.Add(fullNameWithExtension, info);
            }
            else
            {
                if (info.Owner.IsInvalid)
                {
                    Result.Path2FileInfo.Remove(fullNameWithExtension);
                    return null;
                }
            }

            if (!ToCheck.Contains(fullNameWithExtension))
                ToCheck.Add(fullNameWithExtension);
            if (info.Type == FileCompilationType.Unknown)
            {
                info.Type = compilationType;
            }

            if (info.Type == FileCompilationType.JavaScriptAsset)
            {
                if (Result.JavaScriptAssets.AddUnique(info) && _noDependencyCheck)
                {
                    _noDependencyCheck = false;
                }
            }

            return info;
        }

        public void ExpandHtmlHead(string htmlHead)
        {
            var matches = ProjectOptions.ResourceLinkDetector.Matches(htmlHead);
            for (var i = 0; i < matches.Count; i++)
            {
                var m = matches[i];
                var info = AutodetectAndAddDependency(
                    PathUtils.Join(Owner.Owner.FullPath, m.Value.Substring(2, m.Length - 4)), true);
                if (info == null)
                {
                    Owner.Logger.Error("HtmlHead in package.json missing dependency " +
                                       m.Value.Substring(2, m.Length - 4));
                }
            }
        }

        bool _noDependencyCheck;

        public bool CrawlChanges()
        {
            _noDependencyCheck = true;
            foreach (var info in Result.Path2FileInfo)
            {
                CrawlInfo(info.Value);
                if (!_noDependencyCheck)
                {
                    return true;
                }
            }

            return false;
        }

        public void Crawl()
        {
            while (CrawledCount < ToCheck.Count)
            {
                var fileName = ToCheck[(int) CrawledCount];
                CrawledCount++;

                CrawlFile(fileName);
            }
        }

        public TSFileAdditionalInfo CrawlFile(string fileName)
        {
            if (!Result.Path2FileInfo.TryGetValue(fileName, out var info))
            {
                if (_noDependencyCheck)
                {
                    _noDependencyCheck = false;
                }

                var fileCache = Owner.DiskCache.TryGetItem(fileName) as IFileCache;
                if (fileCache == null || fileCache.IsInvalid)
                {
                    if (BuildCtx.Verbose)
                        Owner.Logger.Warn("Crawl skipping missing file " + fileName);
                    return null;
                }

                info = TSFileAdditionalInfo.Create(fileCache, Owner.DiskCache);
                info.Type = FileCompilationType.Unknown;
                Result.Path2FileInfo.Add(fileName, info);
            }
            else
            {
                if (info.Owner.IsInvalid)
                {
                    if (BuildCtx.Verbose)
                        Owner.Logger.Warn("Crawl skipping missing file " + fileName);
                    return null;
                }
            }

            if (_noDependencyCheck)
            {
                if (info.IterationId != IterationId)
                {
                    info.IterationId = IterationId;
                    CrawlInfo(info);
                }
            }
            else
            {
                if (info.Type == FileCompilationType.Unknown)
                {
                    if (IsDts(fileName))
                    {
                        info.Type = FileCompilationType.TypeScriptDefinition;
                    }
                    else if (IsTsOrTsx(fileName))
                    {
                        info.Type = FileCompilationType.TypeScript;
                    }
                    else
                    {
                        var ext = PathUtils.GetExtension(fileName);
                        if (ext.SequenceEqual("css")) info.Type = FileCompilationType.Css;
                        else if (ext.SequenceEqual("js") || ext.SequenceEqual("jsx"))
                            info.Type = FileCompilationType.EsmJavaScript;
                    }
                }

                if (info.IterationId != IterationId)
                {
                    info.IterationId = IterationId;
                    CrawlInfo(info);
                }

                foreach (var dep in info.Dependencies)
                {
                    CheckAdd(dep, FileCompilationType.Unknown);
                }
            }

            return info;
        }

        void CrawlInfo(TSFileAdditionalInfo info)
        {
            if (info.Owner.ChangeId != info.ChangeId)
            {
                info.ChangeId = info.Owner.ChangeId;
                Result.RecompiledIncrementaly.Add(info);
                var oldDependencies = new StructList<string>();
                if (_noDependencyCheck)
                    oldDependencies.TransferFrom(ref info.Dependencies);
                info.StartCompiling();
                switch (info.Type)
                {
                    case FileCompilationType.Json:
                        info.Output = null;
                        info.MapLink = null;
                        info.SourceInfo = null;
                        break;
                    case FileCompilationType.EsmJavaScript:
                    case FileCompilationType.TypeScript:
                        info.HasError = false;
                        if (!TryToResolveFromBuildCache(info))
                        {
                            info.Output = null;
                            info.MapLink = null;
                            info.SourceInfo = null;
                            Transpile(info);
                        }

                        break;
                    case FileCompilationType.JavaScriptAsset:
                    case FileCompilationType.JavaScript:
                        info.Output = info.Owner.Utf8Content;
                        info.MapLink = SourceMap.Identity(info.Output, info.Owner.FullPath);
                        break;
                    case FileCompilationType.Resource:
                        break;
                    case FileCompilationType.Css:
                    case FileCompilationType.ImportedCss:
                        if (!TryToResolveFromBuildCacheCss(info))
                        {
                            var cssProcessor = BuildCtx.CompilerPool.GetCss();
                            try
                            {
                                info.Output = info.Owner.Utf8Content;
                                cssProcessor.ProcessCss(info.Owner.Utf8Content,
                                    ((TSFileAdditionalInfo) info).Owner.FullPath, (string url, string from) =>
                                    {
                                        var urlJustName = url.Split('?', '#')[0];
                                        info.ReportTranspilationDependency(null, urlJustName, null);
                                        return url;
                                    }).Wait();
                            }
                            finally
                            {
                                BuildCtx.CompilerPool.ReleaseCss(cssProcessor);
                            }
                        }

                        ReportDependenciesFromCss(info);
                        break;
                }

                if (_noDependencyCheck)
                {
                    if (!info.Dependencies.SequenceEqual(oldDependencies))
                    {
                        if (BuildCtx.Verbose)
                            Owner.Logger.Info("Dependency change detected " + info.Owner.FullPath);
                        _noDependencyCheck = false;
                    }
                }
            }
        }

        void ReportDependenciesFromCss(TSFileAdditionalInfo info)
        {
            if (info.TranspilationDependencies != null)
                foreach (var dep in info.TranspilationDependencies)
                {
                    var fullJustName = PathUtils.Join(info.Owner.Parent.FullPath, dep.Import);
                    var fileAdditionalInfo =
                        AutodetectAndAddDependency(fullJustName);
                    if (fileAdditionalInfo == null)
                    {
                        info.ReportDiag(true, -3, "Missing dependency " + dep.Import, 0, 0, 0, 0);
                    }

                    info.ReportDependency(fullJustName);
                }
        }

        void Transpile(TSFileAdditionalInfo info)
        {
            ITSCompiler compiler = null;
            try
            {
                compiler = BuildCtx.CompilerPool.GetTs(Owner.DiskCache, BuildCtx.CompilerOptions);
                //_owner.Logger.Info("Transpiling " + info.Owner.FullPath);
                var result = compiler.Transpile(info.Owner.FullPath, info.Owner.Utf8Content);
                if (result.Diagnostics != null)
                {
                    info.ReportDiag(result.Diagnostics);
                    info.HasError = result.Diagnostics.Any(d => d.IsError);
                }
                else
                {
                    info.HasError = false;
                }

                if (info.HasError)
                {
                    info.Output = null;
                    info.MapLink = null;
                }
                else
                {
                    info.Output = SourceMap.RemoveLinkToSourceMap(result.JavaScript);
                    info.MapLink = SourceMap.Parse(result.SourceMap, info.Owner.Parent.FullPath);
                }
            }
            finally
            {
                if (compiler != null)
                    BuildCtx.CompilerPool.ReleaseTs(compiler);
            }

            if (info.HasError)
            {
                info.SourceInfo = null;
                return;
            }

            var backupCurrentlyTranspiling = _currentlyTranspiling;
            try
            {
                if (_currentlyTranspiling == null)
                {
                    _currentlyTranspiling = info;
                }

                var parser = new Parser(new Options(), info.Output);
                var toplevel = parser.Parse();
                toplevel.FigureOutScope();
                var ctx = new ResolvingConstEvalCtx(info.Owner.FullPath, this);

                string resolver(IConstEvalCtx myctx, string text)
                {
                    if (text.StartsWith("project:", StringComparison.Ordinal))
                    {
                        return "project:" + resolver(myctx, text.Substring("project:".Length));
                    }

                    if (text.StartsWith("resource:", StringComparison.Ordinal))
                    {
                        return "resource:" + resolver(myctx, text.Substring("resource:".Length));
                    }

                    if (text.StartsWith("node_modules/", StringComparison.Ordinal))
                    {
                        return ResolveImport(info.Owner.FullPath, text.Substring("node_modules/".Length), false, true);
                    }

                    var res = PathUtils.Join(PathUtils.Parent(myctx.SourceName), text);
                    return res;
                }

                var sourceInfo = GatherBobrilSourceInfo.Gather(toplevel, ctx, resolver);
                info.SourceInfo = sourceInfo;
                AddDependenciesFromSourceInfo(info);
            }
            finally
            {
                _currentlyTranspiling = backupCurrentlyTranspiling;
            }
        }

        public void MakeSourceInfoRelative(SourceInfo sourceInfo, string dir)
        {
            if (sourceInfo == null) return;
            sourceInfo.Assets?.ForEach(a =>
            {
                if (a.Name == null)
                {
                    a.RelativeName = null;
                    return;
                }

                a.RelativeName = ToRelativeName(a.Name, dir);
            });
            sourceInfo.Sprites?.ForEach(s =>
            {
                if (s.Name == null)
                {
                    s.RelativeName = null;
                    return;
                }

                s.RelativeName = ToRelativeName(s.Name, dir);
            });
        }

        string ToRelativeName(string name, string dir)
        {
            if (name.StartsWith("resource:"))
            {
                return "resource:" + ToRelativeName(name.Substring(9), dir);
            }

            if (PathUtils.IsAnyChildOf(name, dir))
            {
                var p = PathUtils.Subtract(name, dir);
                if (!p.StartsWith("node_modules/") && !p.Contains("/node_modules/"))
                    return "./" + p;
            }

            if (name.Contains("/node_modules/"))
            {
                return name.Split("/node_modules/").Last();
            }

            return "./" + PathUtils.Subtract(name, dir);
        }

        public bool MakeSourceInfoAbsolute(SourceInfo sourceInfo, string from)
        {
            if (sourceInfo == null) return true;
            bool ok = true;
            sourceInfo.Assets?.ForEach(a =>
            {
                if (a.RelativeName == null)
                {
                    if (a.Name != null)
                    {
                        ok = false;
                    }

                    return;
                }

                a.Name = ToAbsoluteName(a.RelativeName, from, ref ok);
            });
            sourceInfo.Sprites?.ForEach(s =>
            {
                if (s.RelativeName == null)
                {
                    if (s.Name != null)
                    {
                        ok = false;
                    }

                    return;
                }

                s.Name = ToAbsoluteName(s.RelativeName, from, ref ok);
            });
            return ok;
        }

        string ToAbsoluteName(string relativeName, string from, ref bool ok)
        {
            if (relativeName.StartsWith("resource:"))
            {
                return "resource:" + ToAbsoluteName(relativeName.Substring(9), from, ref ok);
            }

            var res = ResolveImport(from, relativeName, false, true);
            if (res == null || res == "?")
            {
                ok = false;
                return relativeName;
            }

            return res;
        }

        public void AddDependenciesFromSourceInfo(TSFileAdditionalInfo fileInfo)
        {
            var sourceInfo = fileInfo.SourceInfo;
            if (sourceInfo == null)
                return;
            sourceInfo.Imports?.ForEach(i =>
            {
                var resolved = ResolveImport(fileInfo.Owner.FullPath, i.Name);
                if (resolved != null && resolved != "?")
                {
                    fileInfo.ReportDependency(resolved);
                }
                else
                {
                    fileInfo.ReportDiag(true, -3, "Missing import " + i.Name, i.StartLine, i.StartCol, i.EndLine,
                        i.EndCol);
                }
            });
            sourceInfo.Assets?.ForEach(a =>
            {
                if (a.Name == null)
                {
                    fileInfo.ReportDiag(true, -5, "First parameter of b.asset must be resolved as constant string", a.StartLine, a.StartCol, a.EndLine,
                        a.EndCol);
                    return;
                }
                var assetName = a.Name;
                if (assetName.StartsWith("project:"))
                {
                    assetName = assetName.Substring(8) + "/package.json";
                    if (!(Owner.DiskCache.TryGetItem(PathUtils.Join(Owner.Owner.FullPath, assetName)) is
                        IFileCache))
                    {
                        fileInfo.ReportDiag(true, -3, "Missing dependency " + assetName, a.StartLine, a.StartCol,
                            a.EndLine, a.EndCol);
                    }
                }
                else if (assetName.StartsWith("resource:"))
                {
                    assetName = assetName.Substring(9);
                    if (ReportDependency(fileInfo, AutodetectAndAddDependency(assetName, true)) == null)
                    {
                        fileInfo.ReportDiag(true, -3, "Missing dependency " + assetName, a.StartLine, a.StartCol,
                            a.EndLine, a.EndCol);
                    }
                }
                else
                {
                    if (ReportDependency(fileInfo, AutodetectAndAddDependency(assetName)) == null)
                    {
                        fileInfo.ReportDiag(true, -3, "Missing dependency " + assetName, a.StartLine, a.StartCol,
                            a.EndLine, a.EndCol);
                    }
                }
            });
            if (sourceInfo.Sprites != null)
            {
                if (Owner.ProjectOptions.SpriteGeneration)
                {
                    var spriteHolder = Owner.ProjectOptions.SpriteGenerator;
                    spriteHolder.Process(sourceInfo.Sprites);
                }
                else
                {
                    sourceInfo.Sprites.ForEach(s =>
                    {
                        if (s.Name == null)
                            return;
                        var assetName = s.Name;
                        if (ReportDependency(fileInfo, AutodetectAndAddDependency(assetName)) == null)
                        {
                            fileInfo.ReportDiag(true, -3, "Missing dependency " + assetName, s.NameStartLine,
                                s.NameStartCol, s.NameEndLine, s.NameEndCol);
                        }
                    });
                }
            }

            if (sourceInfo.VdomTranslations != null)
            {
                var trdb = Owner.ProjectOptions.TranslationDb;
                if (trdb != null)
                {
                    sourceInfo.VdomTranslations.ForEach(t =>
                    {
                        if (t.Message == null)
                            return;
                        var err = trdb.CheckMessage(t.Message, t.KnownParams);
                        if (err != null)
                        {
                            fileInfo.ReportDiag(false, -7,
                                "Problem with translation message \"" + t.Message + "\" " + err, t.StartLine,
                                t.StartCol, t.EndLine, t.EndCol);
                        }
                    });
                }
            }

            if (sourceInfo.Translations != null)
            {
                var trdb = Owner.ProjectOptions.TranslationDb;
                if (trdb != null)
                {
                    sourceInfo.Translations.ForEach(t =>
                    {
                        if (t.Message == null)
                            return;
                        if (t.WithParams)
                        {
                            var err = trdb.CheckMessage(t.Message, t.KnownParams);
                            if (err != null)
                            {
                                fileInfo.ReportDiag(false, -7,
                                    "Problem with translation message \"" + t.Message + "\" " + err, t.StartLine,
                                    t.StartCol, t.EndLine, t.EndCol);
                            }
                        }
                    });
                }
            }
        }

        TSFileAdditionalInfo ReportDependency(TSFileAdditionalInfo owner, TSFileAdditionalInfo dep)
        {
            if (dep != null)
            {
                owner.ReportDependency(dep.Owner.FullPath);
            }

            return dep;
        }

        TSFileAdditionalInfo AutodetectAndAddDependency(string depName, bool forceResource = false)
        {
            if (forceResource)
            {
                return CheckAdd(depName, FileCompilationType.Resource);
            }

            if (depName.EndsWith(".js", StringComparison.Ordinal))
            {
                return CheckAdd(depName, FileCompilationType.JavaScriptAsset);
            }

            if (depName.EndsWith(".css", StringComparison.Ordinal))
            {
                return CheckAdd(depName, FileCompilationType.Css);
            }

            var res = CheckAdd(depName, FileCompilationType.Unknown);
            if (res != null)
            {
                if (res.Type == FileCompilationType.Unknown)
                {
                    res.Type = FileCompilationType.Resource;
                }
            }

            return res;
        }

        Dictionary<string, AstToplevel> _parsedCache = new Dictionary<string, AstToplevel>();

        public (string fileName, AstToplevel content) ResolveAndLoad(JsModule module)
        {
            var fileName = ResolveImport(module.ImportedFrom, module.Name);
            if (fileName == null || fileName == "?") return (null, null);
            if (_currentlyTranspiling.Owner.FullPath == fileName)
            {
                return (null, null);
            }

            Result.Path2FileInfo.TryGetValue(module.ImportedFrom, out var sourceInfo);
            var info = CrawlFile(fileName);
            _currentlyTranspiling.ReportTranspilationDependency(sourceInfo.Owner.HashOfContent, module.Name,
                info.Owner.HashOfContent);
            if (_parsedCache.TryGetValue(fileName, out var toplevel))
            {
                return (fileName, toplevel);
            }

            if (info.Type == FileCompilationType.Json)
            {
                _parsedCache.Add(fileName, null);
                return (fileName, null);
            }

            try
            {
                var parser = new Parser(new Options(), info.Output);
                toplevel = parser.Parse();
                toplevel.FigureOutScope();
                _parsedCache.Add(fileName, toplevel);
                return (fileName, toplevel);
            }
            catch
            {
                return (fileName, null);
            }
        }
    }
}
