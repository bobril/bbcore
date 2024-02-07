using Lib.DiskCache;
using Lib.Utils;
using System;
using System.Linq;
using Lib.Composition;
using Njsast.SourceMap;
using Njsast;
using Njsast.Reader;
using Njsast.ConstEval;
using Njsast.Bobril;
using Njsast.Ast;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using BobrilMdx;
using HtmlAgilityPack;
using Lib.BuildCache;
using Njsast.Runtime;
using WebMarkupMin.Core;

namespace Lib.TSCompiler;

public class BuildModuleCtx : IImportResolver
{
    public BuildCtx? BuildCtx;
    public TSProject? Owner;
    public BuildResult? Result;
    public MainBuildResult? MainResult;
    public int IterationId;
    TsFileAdditionalInfo? _currentlyTranspiling;

    static readonly string[] ExtensionsToImport = { ".tsx", ".ts", ".d.ts", ".jsx", ".js", "" };
    static readonly string[] ExtensionsToImportFromJs = { ".jsx", ".js", "" };

    internal OrderedHashSet<string>? ToCheck;
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

    static bool IsMdxb(ReadOnlySpan<char> name)
    {
        return name.EndsWith(".mdxb");
    }

    public TSProject? ResolveModule(string name)
    {
        if (Result!.Modules.TryGetValue(name, out var module))
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
        var dir = Owner!.Owner.FullPath;
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

                module = TSProject.Create(dc, Owner.DiskCache, Owner.Logger, name);
                module!.LoadProjectJson(true, Owner.ProjectOptions);
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
        return Owner!.DiskCache.TryGetItem(name) is IFileCache { IsInvalid: false };
    }

    public bool CheckItemExistence(string name)
    {
        return Owner!.DiskCache.TryGetItem(name)?.IsInvalid == false;
    }

    public void ReportMissingImport(string from, string name)
    {
        if (Result!.Path2FileInfo.TryGetValue(from, out var parentInfo))
        {
            parentInfo.ReportDiag(true, -15, "Cannot resolve import '" + name + "'", 0, 0, 0, 0);
        }
    }

    // returns "?" if error in resolving
    public string ResolveImport(string from, string name, bool preferDts = false, bool isAsset = false,
        bool forceResource = false, bool skipCheckAdd = false, FileCompilationType forceCompilationType = FileCompilationType.Unknown)
    {
        if (forceCompilationType == FileCompilationType.Resource) forceResource = true;
        if (Result!.ResolveCache.TryGetValue((from, name), out var res))
        {
            if (res.IterationId == IterationId) return res.FileName!;
        }

        if (res == null)
        {
            res = new();
            Result.ResolveCache.Add((from, name), res);
        }
        else
        {
            res.FileName = null;
            res.FileNameJs = null;
        }

        if (Owner?.ProjectOptions?.Imports?.TryGetValue(name, out var import) ?? false)
        {
            if (import == null)
            {
                res.FileName = "<empty>";
                return res.FileName;
            }
            name = import;
        }

        res.IterationId = IterationId;
        var relative = name.StartsWith("./") || name.StartsWith("../");
        Result.Path2FileInfo.TryGetValue(from, out var parentInfo);
        string? fn = null;
        if (relative)
        {
            fn = PathUtils.Join(parentInfo.Owner!.Parent!.FullPath, name);
            var browserResolve = parentInfo.FromModule?.ProjectOptions.BrowserResolve;
            if (browserResolve != null)
            {
                var relativeToModule = PathUtils.Subtract(fn + ".js", parentInfo.FromModule!.Owner.FullPath);
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
            if (forceCompilationType != FileCompilationType.Unknown)
            {
                if (Owner!.DiskCache.TryGetItem(fn) is IFileCache { IsInvalid: false })
                {
                    res.FileName = fn;
                    CheckAdd(fn, forceCompilationType);
                    return res.FileName;
                }
            }
            if (fn.EndsWith(".json") || fn.EndsWith(".css"))
            {
                if (Owner!.DiskCache.TryGetItem(fn) is IFileCache { IsInvalid: false })
                {
                    res.FileName = fn;
                    CheckAdd(fn, forceResource
                        ? FileCompilationType.Resource
                        : fn.EndsWith(".json")
                            ? isAsset ? FileCompilationType.Resource : FileCompilationType.Json
                            : isAsset ? FileCompilationType.Css : FileCompilationType.ImportedCss);
                    return res.FileName;
                }
            }

            var dirPath = PathUtils.Parent(fn).ToString();
            var fileOnly = fn.Substring(dirPath.Length + 1);
            if (Owner!.DiskCache.TryGetItem(dirPath) is not IDirectoryCache dc || dc.IsInvalid)
            {
                res.FileName = "?";
                return res.FileName;
            }

            if (fileOnly == ".mdxb")
            {
                if (!Result.Path2FileInfo.TryGetValue(fn, out var fai))
                {
                    fai = TsFileAdditionalInfo.CreateVirtual(dc);
                    Result.Path2FileInfo.GetOrAddValueRef(fn) = fai;
                }

                fai.Type = FileCompilationType.MdxbList;
                fai.DirOwner = dc;
                CrawlInfo(fai);
            }
            else if (IsMdxb(fn))
            {
                if (dc.TryGetChild(fileOnly) is not IFileCache fc || fc.IsInvalid)
                {
                    res.FileName = "?";
                    return res.FileName;
                }

                var info = CheckAdd(fn, FileCompilationType.Mdxb);
                if (info != null) CrawlInfo(info);
            }

            var item = (parentInfo.Type == FileCompilationType.EsmJavaScript
                    ? ExtensionsToImportFromJs
                    : ExtensionsToImport).Select(ext =>
                {
                    if (dc.TryGetChild(fileOnly + ext) is not IFileCache ff || ff.IsInvalid)
                    {
                        return null;
                    }

                    return ff;
                })
                .FirstOrDefault(i => i != null);
            if (item == null)
            {
                if (dc.TryGetChild(fileOnly) is not IDirectoryCache dc2 || dc2.IsInvalid)
                {
                    res.FileName = "?";
                    return res.FileName;
                }

                Owner.DiskCache.UpdateIfNeeded(dc2);
                if (dc2.TryGetChild("package.json") is IFileCache { IsInvalid: false })
                {
                    var mn = PathUtils.Subtract(fn, Owner.Owner.FullPath);
                    if (!Result!.Modules.TryGetValue(mn, out var module))
                    {
                        module = TSProject.Create(dc2, Owner.DiskCache, Owner.Logger, mn);
                        module!.LoadProjectJson(true, Owner.ProjectOptions);
                        Result!.Modules[mn] = module;
                    }

                    var mainFile = PathUtils.Join(module.Owner.FullPath, module.MainFile);
                    res.FileName = mainFile;
                    if (!skipCheckAdd)
                    {
                        CheckAdd(mainFile,
                            IsTsOrTsx(mainFile)
                                ? FileCompilationType.TypeScript
                                : FileCompilationType.EsmJavaScript);
                    }

                    return res.FileName;
                }

                fn += "/index";
                goto relative;
            }

            res.FileName = item.FullPath;
            if (item.FullPath.Substring(0, fn.Length) != fn)
            {
                parentInfo.ReportDiag(false, -1,
                    "Local import has wrong casing '" + fn + "' on disk '" + item.FullPath + "'", 0, 0, 0, 0);
            }

            if (!skipCheckAdd)
            {
                if (forceResource)
                {
                    CheckAdd(item.FullPath, FileCompilationType.Resource);
                }
                else if (IsDts(item.Name))
                {
                    CheckAdd(item.FullPath, FileCompilationType.TypeScriptDefinition);
                    if (dc.TryGetChild(fileOnly + ".js") is IFileCache jsItem)
                    {
                        CheckAdd(jsItem.FullPath, FileCompilationType.EsmJavaScript);
                        res.FileNameJs = jsItem.FullPath;
                        parentInfo.ReportDependency(jsItem.FullPath);
                    }
                    // else implementation for .d.ts file does not have same name, it needs to be added to build by b.asset("lib.js") and cannot have dependencies
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
            }

            return res.FileNameWithPreference(preferDts) ?? "?";
        }
        else
        {
            var pos = 0;
            PathUtils.EnumParts(name, ref pos, out var mn, out _);
            string moduleName;
            if (name[0] == '@')
            {
                PathUtils.EnumParts(name, ref pos, out var mn2, out _);
                moduleName = mn.ToString() + "/" + mn2.ToString();
            }
            else
            {
                moduleName = mn.ToString();
            }

            string? mainFileReplace = null;
            if (moduleName.Length == name.Length && parentInfo != null)
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
                            fn = PathUtils.Join(parentInfo.FromModule!.Owner.FullPath, resolveReplace);
                            relative = true;
                            goto relative;
                        }

                        mainFileReplace = resolveReplace.Substring(name.Length + 1);
                    }
                }
            }

            var moduleInfo = ResolveModule(moduleName);
            if (moduleInfo == null)
            {
                ReportMissingImport(from, name);
                res.FileName = "?";
                return res.FileName;
            }

            if (parentInfo!.FromModule == Owner)
            {
                if (!Owner.Dependencies?.Contains(moduleInfo.Name!) ?? false)
                {
                    var allowDevDependencies =
                            (Owner.ProjectOptions.ExampleSources?.Contains(parentInfo.Owner!.FullPath) ?? false) ||
                            (Owner.ProjectOptions.TestSources?.Contains(parentInfo.Owner!.FullPath) ?? false) ||
                            IsExampleOrSpecDir(Owner.Owner, parentInfo.Owner!.Parent!)
                        ;
                    if (!allowDevDependencies || (!Owner.DevDependencies?.Contains(moduleInfo.Name!) ?? false))
                    {
                        parentInfo.ReportDiag(false, -12,
                            $"Importing module {moduleInfo.Name} without being in package.json as dependency", 0, 0,
                            0, 0);
                    }
                }
            }

            if (moduleName != moduleInfo.Name)
            {
                parentInfo.ReportDiag(false, -2,
                    "Module import has wrong casing '" + moduleName + "' on disk '" + moduleInfo.Name + "'", 0, 0,
                    0, 0);
            }

            if (moduleName.Length != name.Length)
            {
                fn = PathUtils.Join(moduleInfo.Owner.FullPath, name.Substring(moduleName.Length + 1));
                relative = true;
                goto relative;
            }

            if (mainFileReplace != null)
            {
                moduleInfo.MainFile = mainFileReplace;
            }

            var mainFile = PathUtils.Join(moduleInfo.Owner.FullPath, moduleInfo.MainFile);
            res.FileName = mainFile;
            if (!skipCheckAdd)
            {
                CheckAdd(mainFile,
                    IsTsOrTsx(mainFile) ? FileCompilationType.TypeScript : FileCompilationType.EsmJavaScript);
            }

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

    static bool IsExampleOrSpecDir(IDirectoryCache rootProjectDir, IDirectoryCache? dir)
    {
        while (dir != null)
        {
            if (IsExampleOrSpecDir(dir.Name)) return true;
            dir = dir.Parent;
            if (dir == rootProjectDir) break;
        }

        return false;
    }

    static bool IsExampleOrSpecDir(string name)
    {
        return name.Equals("example", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("examples", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("test", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("tests", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("spec", StringComparison.OrdinalIgnoreCase);
    }

    bool TryToResolveFromBuildCache(TsFileAdditionalInfo itemInfo)
    {
        itemInfo.TakenFromBuildCache = false;
        var bc = BuildCtx!.BuildCache;
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
        processed.RepeatAdd(false, (uint)transpilationDependencies.Count);
        bool somethingFailed;
        do
        {
            var somethingProcessed = false;
            somethingFailed = false;
            for (var i = 0u; i < processed.Count; i++)
            {
                if (processed[i]) continue;
                var dep = transpilationDependencies[(int)i];
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

    bool TryToResolveFromBuildCacheCss(TsFileAdditionalInfo itemInfo)
    {
        itemInfo.TakenFromBuildCache = false;
        var bc = BuildCtx!.BuildCache;
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
        var bc = BuildCtx!.BuildCache;
        foreach (var f in result.RecompiledIncrementally)
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
                case FileCompilationType.Scss:
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

    public TsFileAdditionalInfo? CheckAdd(string fullNameWithExtension, FileCompilationType compilationType)
    {
        if (!Result.Path2FileInfo.TryGetValue(fullNameWithExtension, out var info))
        {
            if (Owner.DiskCache.TryGetItem(fullNameWithExtension) is not IFileCache fc || fc.IsInvalid) return null;
            info = TsFileAdditionalInfo.Create(fc);
            info.Type = compilationType;
            MainResult.MergeCommonSourceDirectory(fc.FullPath);
            Result.Path2FileInfo.GetOrAddValueRef(fullNameWithExtension) = info;
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
        try
        {
            foreach (var info in Result.Path2FileInfo)
            {
                CrawlInfo(info.Value);
                if (!_noDependencyCheck)
                {
                    return true;
                }
            }
        }
        catch (InvalidOperationException)
        {
            return true;
        }

        return false;
    }

    public void Crawl()
    {
        while (CrawledCount < ToCheck.Count)
        {
            var fileName = ToCheck[(int)CrawledCount];
            CrawledCount++;

            CrawlFile(fileName);
        }
    }

    public TsFileAdditionalInfo? CrawlFile(string fileName)
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

            info = TsFileAdditionalInfo.Create(fileCache);
            info.Type = FileCompilationType.Unknown;
            Result.Path2FileInfo.GetOrAddValueRef(fileName) = info;
        }
        else
        {
            if (info.Owner is { IsInvalid: true })
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
                    else if (ext.SequenceEqual("scss")) info.Type = FileCompilationType.Scss;
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

    void CrawlInfo(TsFileAdditionalInfo info)
    {
        if (info.DetectChange())
        {
            Result.RecompiledIncrementally.Add(info);
            var oldDependencies = new StructList<string>();
            if (_noDependencyCheck)
                oldDependencies.TransferFrom(ref info.Dependencies);
            info.StartCompiling();
            var sw = Stopwatch.StartNew();
            switch (info.Type)
            {
                case FileCompilationType.MdxbList:
                {
                    var newContentList = BuildMdxbList(info.DirOwner, Owner.DiskCache);
                    var trueChangeDetected =
                        Owner.DiskCache.UpdateFile(info.DirOwner.FullPath + "/.mdxb.tsx", newContentList);
                    Owner.Logger.Info((trueChangeDetected ? "Updated" : "Checked") + " " + info.DirOwner.FullPath +
                                      "/.mdxb.tsx in " + sw.ElapsedMilliseconds + "ms");
                    break;
                }
                case FileCompilationType.Mdxb:
                {
                    var mdxToTsx = new MdxToTsx();
                    var content = info.Owner.Utf8Content;
                    var newContent = UpdateMdxDependentFileContent(content, info.Owner, Owner.DiskCache);
                    if (newContent != content)
                    {
                        Owner.DiskCache.UpdateFile(info.Owner.FullPath, newContent);
                        Owner.Logger.Info("Updated Source in " + info.Owner.FullPath);
                    }

                    mdxToTsx.Parse(newContent);
                    var trueChangeDetected =
                        Owner.DiskCache.UpdateFile(info.Owner.FullPath + ".tsx", mdxToTsx.Render().content);
                    Owner.Logger.Info((trueChangeDetected ? "Updated" : "Checked") + " " + info.Owner.FullPath +
                                      " in " + sw.ElapsedMilliseconds + "ms");
                    break;
                }
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
                case FileCompilationType.Html:
                    if (!TryToResolveFromBuildCacheCss(info))
                    {
                        var htmlContent = info.Owner.Utf8Content;
                        var htmlDoc = new HtmlDocument();
                        htmlDoc.LoadHtml(htmlContent);
                        var toReplace = new StructList<(HtmlNode, string)>();
                        foreach (var htmlNode in htmlDoc.DocumentNode.Descendants("script"))
                        {
                            if (htmlNode.Attributes.AttributesWithName("src").SingleOrDefault() is { } srcattr)
                            {
                                var scriptPath = PathUtils.Join( PathUtils.Parent(info.Owner.FullPath), srcattr.Value);
                                if (Owner!.DiskCache.TryGetItem(scriptPath) is IFileCache { IsInvalid: false } fc)
                                {
                                    info.ReportTranspilationDependency(info.Owner.HashOfContent, scriptPath, fc.HashOfContent);
                                    toReplace.Add((htmlNode,"<script>"+fc.Utf8Content+"</script>"));
                                }
                                else
                                {
                                    info.ReportDiag(true, -3, "Missing dependency " + srcattr.Value, srcattr.Line, srcattr.LinePosition, srcattr.Line, srcattr.LinePosition);
                                }
                            }
                        }

                        if (toReplace.Count > 0)
                        {
                            foreach (var (htmlNode, newHtml) in toReplace)
                            {
                                htmlNode.Attributes.RemoveAll();
                                htmlNode.ChildNodes.Clear();
                                htmlNode.ParentNode.ReplaceChild(HtmlNode.CreateNode(newHtml), htmlNode);
                            }
                            htmlContent = htmlDoc.DocumentNode.WriteContentTo();
                        }
                        
                        htmlContent = new HtmlMinifier().Minify(htmlContent).MinifiedContent;
                        info.Output = htmlContent;
                    }

                    ReportDependenciesFromCss(info);
                    break;
                case FileCompilationType.Scss:
                    if (!TryToResolveFromBuildCacheCss(info))
                    {
                        var scssProcessor = BuildCtx.CompilerPool.GetScss();
                        string cssContent;
                        try
                        {
                            info.Output = info.Owner!.Utf8Content;
                            var fullPath = info.Owner.FullPath;
                            var prepend = fullPath.StartsWith("/") ? "" : fullPath[..2];

                            cssContent = scssProcessor.ProcessScss(info.Owner.Utf8Content,
                                "file://" + info.Owner.FullPath[prepend.Length..], url =>
                                {
                                    if (url.StartsWith("file://")) url = prepend + url[7..];
                                    if (Owner!.DiskCache.TryGetItem(url) is IFileCache { IsInvalid: false })
                                    {
                                        return "file://" + url[prepend.Length..];
                                    }

                                    if (Owner.DiskCache.TryGetItem(url + ".scss") is IFileCache { IsInvalid: false })
                                    {
                                        return "file://" + url[prepend.Length..] + ".scss";
                                    }

                                    if (Owner.DiskCache.TryGetItem(url + ".css") is IFileCache { IsInvalid: false })
                                    {
                                        return "file://" + url[prepend.Length..] + ".css";
                                    }

                                    return "file://" + url;
                                }, url =>
                                {
                                    if (url.StartsWith("file://")) url = prepend + url[7..];
                                    if (Owner!.DiskCache.TryGetItem(url) is IFileCache { IsInvalid: false } fc)
                                    {
                                        info.ReportTranspilationDependency(info.Owner.HashOfContent, url, fc.HashOfContent);
                                        return fc.Utf8Content;
                                    }

                                    info.ReportDiag(true, -3, "Missing dependency " + url, 1, 1, 1, 1);
                                    return "";
                                }, text => { Owner!.Logger.Info(text); }).Result;
                        }
                        finally
                        {
                            BuildCtx.CompilerPool.ReleaseScss(scssProcessor);
                        }

                        var cssProcessor = BuildCtx.CompilerPool.GetCss();
                        try
                        {
                            info.Output = cssContent;
                            cssProcessor.ProcessCss(cssContent,
                                info.Owner.FullPath, (url, @from) =>
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

                        info.Output = "\"use strict\"; const lit = require(\"lit\"); exports.default = lit.css`" +
                                      info.Output.Replace("\\", "\\\\").Replace("$", "\\$").Replace("`", "\\`") + "`;";
                    }
                    var resolved = ResolveImport(info.Owner.FullPath, "lit");
                    if (resolved != null && resolved != "?")
                    {
                        info.ReportDependency(resolved);
                    }
                    else
                    {
                        info.ReportDiag(true, -3, "Missing import lit", 1, 1, 1, 1);
                    }

                    ReportDependenciesFromCss(info);
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
                                info.Owner.FullPath, (url, @from) =>
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

    string BuildMdxbList(IDirectoryCache dir, IDiskCache diskCache)
    {
        var files = FindAllMdxbs(dir).OrderBy(i => i.FullPath).ToArray();
        var sb = new StringBuilder();
        sb.Append("const r = [\n");
        foreach (var fc in files)
        {
            var mdxToTsx = new MdxToTsx();
            var content = fc.Utf8Content;
            mdxToTsx.Parse(content);
            sb.Append("  [()=>import(\"./");
            sb.Append(PathUtils.Subtract(fc.FullPath, dir.FullPath));
            sb.Append("\").then(m=>m.default),");
            sb.Append(TypeConverter.ToAst(mdxToTsx.Render().metadata).PrintToString());
            sb.Append("],\n");
        }

        sb.Append("] as const;\n");
        sb.Append("export default r;\n");
        return sb.ToString();
    }

    static IEnumerable<IFileCache> FindAllMdxbs(IDirectoryCache dir)
    {
        foreach (var itemCache in dir)
        {
            if (itemCache is IFileCache { IsInvalid: false } fc && fc.Name.EndsWith(".mdxb"))
            {
                yield return fc;
            }

            if (itemCache is IDirectoryCache { IsInvalid: false } dc)
            {
                foreach (var i in FindAllMdxbs(dc))
                {
                    yield return i;
                }
            }
        }
    }

    static string UpdateMdxDependentFileContent(string content, IFileCache owner, IDiskCache diskCache)
    {
        content = content.Replace("\r\n", "\n");
        var change = false;
        var lines = content.Split('\n').ToList();
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.StartsWith("```") && line.Contains("from:"))
            {
                var endLine = i + 1;
                while (endLine < lines.Count && lines[endLine] != "```") endLine++;
                if (endLine == lines.Count) break;
                var pos = line.Split("from:")[1].Split(':');
                var fn = PathUtils.Join(owner.Parent.FullPath, pos[0]);
                if (diskCache.TryGetItem(fn) is IFileCache { IsInvalid: false } file)
                {
                    var nc = file.Utf8Content;
                    nc = nc.Replace("\r\n", "\n");
                    if (nc.EndsWith('\n')) nc = nc[..^1];
                    if (pos.Length > 1)
                    {
                        if (!int.TryParse(pos[1], out var startLine)) continue;
                        if (startLine < 1) continue;
                        nc = nc.Split('\n').Skip(startLine - 1).FirstOrDefault() ?? "";
                        if (pos.Length > 2)
                        {
                            if (!int.TryParse(pos[2], out var startCol)) continue;
                            if (startCol < 1 || startCol >= nc.Length) continue;
                            nc = nc.Substring(startCol - 1);
                        }
                    }

                    var newLines = nc.Split('\n');
                    if (!lines.Skip(i + 1).Take(endLine - i - 1).SequenceEqual(newLines))
                    {
                        lines.RemoveRange(i + 1, endLine - i - 1);
                        lines.InsertRange(i + 1, newLines);
                        change = true;
                    }

                    i += newLines.Length;
                }
            }
        }

        if (change)
        {
            content = string.Join('\n', lines);
        }

        return content;
    }

    void ReportDependenciesFromCss(TsFileAdditionalInfo info)
    {
        if (info.TranspilationDependencies != null)
            foreach (var dep in info.TranspilationDependencies)
            {
                if (dep.TargetHash != null) continue;
                var fullJustName = PathUtils.Join(info.Owner!.Parent!.FullPath, dep.Import!);
                var fileAdditionalInfo =
                    AutodetectAndAddDependency(fullJustName);
                if (fileAdditionalInfo == null)
                {
                    info.ReportDiag(true, -3, "Missing dependency " + dep.Import, 0, 0, 0, 0);
                }

                info.ReportDependency(fullJustName);
            }
    }

    void Transpile(TsFileAdditionalInfo info)
    {
        ITSCompiler compiler = null;
        try
        {
            if (info.Owner.IsInvalid)
            {
                info.HasError = true;
                info.Output = null;
                info.MapLink = null;
                info.SourceInfo = null;
                info.ReportDiag(true, -18, "File does not exists", 0, 0, 0, 0);
                return;
            }

            var fileName = info.Owner.FullPath;
            var source = info.Owner.Utf8Content;
            var transpileOptions = BuildCtx!.CompilerOptions.Clone();
            transpileOptions.module = ModuleKind.Commonjs;
            transpileOptions.moduleResolution = ModuleResolutionKind.Node10;
            compiler = BuildCtx.CompilerPool.GetTs(Owner!.DiskCache, transpileOptions);
            //_owner.Logger.Info("Transpiling " + info.Owner.FullPath);
            var result = compiler.Transpile(fileName, source);
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

            var parser = new Parser(new(), info.Output);
            var toplevel = parser.Parse();
            toplevel.FigureOutScope();
            var ctx = new ResolvingConstEvalCtx(info.Owner.FullPath, this);

            string Resolver(IConstEvalCtx myctx, string text)
            {
                return ResolverWithPossibleForcingResource(myctx, text, FileCompilationType.Unknown);
            }

            string ResolverWithPossibleForcingResource(IConstEvalCtx myctx, string text, FileCompilationType forceCompilationType)
            {
                if (text.StartsWith("project:", StringComparison.Ordinal))
                {
                    var (pref, name) = SplitProjectAssetName(text);
                    return pref + ResolverWithPossibleForcingResource(myctx, name, FileCompilationType.Resource);
                }

                if (text.StartsWith("resource:", StringComparison.Ordinal))
                {
                    return "resource:" +
                           ResolverWithPossibleForcingResource(myctx, text.Substring("resource:".Length), FileCompilationType.Resource);
                }

                if (text.StartsWith("html:", StringComparison.Ordinal))
                {
                    return "html:" +
                           ResolverWithPossibleForcingResource(myctx, text.Substring("html:".Length), FileCompilationType.Html);
                }
                
                if (text.StartsWith("node_modules/", StringComparison.Ordinal))
                {
                    var res2 = ResolveImport(info.Owner.FullPath, text.Substring("node_modules/".Length), false,
                        true, false, true, forceCompilationType);
                    return res2 == "?" ? text : res2;
                }

                var res = PathUtils.Join(PathUtils.Parent(myctx.SourceName), text);
                return res;
            }

            var sourceInfo = GatherBobrilSourceInfo.Gather(toplevel, ctx, Resolver);
            info.SourceInfo = sourceInfo;
            AddDependenciesFromSourceInfo(info);
        }
        catch (SyntaxError error)
        {
            var pos = info.MapLink?.FindPosition(error.Position.Line, error.Position.Column) ??
                      new SourceCodePosition { Line = error.Position.Line, Col = error.Position.Column };
            info.ReportDiag(true, -16, error.Message, pos.Line, pos.Col, pos.Line, pos.Col);
            info.SourceInfo = null;
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

    static string ToRelativeName(string name, string dir)
    {
        if (name.StartsWith("project:"))
        {
            var (pref, n) = SplitProjectAssetName(name);
            return pref + ToRelativeName(n, dir);
        }

        if (name.StartsWith("resource:"))
        {
            return "resource:" + ToRelativeName(name.Substring(9), dir);
        }

        if (name.StartsWith("html:"))
        {
            return "html:" + ToRelativeName(name.Substring(5), dir);
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

    string ToAbsoluteName(string relativeName, string from, ref bool ok, FileCompilationType forceCompilationType = FileCompilationType.Unknown)
    {
        if (relativeName.StartsWith("project:"))
        {
            var (pref, name) = SplitProjectAssetName(relativeName);
            return pref + ToAbsoluteName(name, from, ref ok, FileCompilationType.Resource);
        }

        if (relativeName.StartsWith("resource:"))
        {
            return "resource:" + ToAbsoluteName(relativeName.Substring(9), from, ref ok, FileCompilationType.Resource);
        }

        if (relativeName.StartsWith("html:"))
        {
            return "html:" + ToAbsoluteName(relativeName.Substring(5), from, ref ok, FileCompilationType.Html);
        }
        
        var res = ResolveImport(from, relativeName, false, true, forceCompilationType: forceCompilationType);
        if (res is null or "?")
        {
            ok = false;
            return relativeName;
        }

        return res;
    }

    public static (string Prefix, string Path) SplitProjectAssetName(string assetName)
    {
        if (!assetName.StartsWith("project:", StringComparison.Ordinal))
        {
            return ("", assetName);
        }

        var colonIdx = assetName.IndexOf(':', 8);
        if (colonIdx < 11)
        {
            return ("project:", assetName[8..]);
        }

        return (assetName[..(colonIdx + 1)], assetName[(colonIdx + 1)..]);
    }

    public void AddDependenciesFromSourceInfo(TsFileAdditionalInfo fileInfo)
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
                fileInfo.ReportDiag(true, -5, "First parameter of b.asset must be resolved as constant string",
                    a.StartLine, a.StartCol, a.EndLine,
                    a.EndCol);
                return;
            }

            var assetName = a.Name;
            if (assetName.StartsWith("project:"))
            {
                var (pref, name) = SplitProjectAssetName(assetName);
                if (pref.Length == 8)
                {
                    name += "/package.json";
                }

                if (Owner!.DiskCache.TryGetItem(PathUtils.Join(Owner.Owner.FullPath, name)) is not IFileCache)
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
            else if (assetName.StartsWith("html:"))
            {
                assetName = assetName.Substring(5);
                if (ReportDependency(fileInfo, CheckAdd(assetName, FileCompilationType.Html)) == null)
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
            foreach (var sourceInfoSprite in sourceInfo.Sprites)
            {
                if (sourceInfoSprite.IsSvg())
                {
                    var name = sourceInfoSprite.Name;
                    if (!(Owner.DiskCache.TryGetItem(PathUtils.Join(Owner.Owner.FullPath, name)) is
                            IFileCache fc))
                    {
                        fileInfo.ReportDiag(true, -3, "Missing dependency " + name, sourceInfoSprite.StartLine,
                            sourceInfoSprite.StartCol,
                            sourceInfoSprite.EndLine, sourceInfoSprite.EndCol);
                    }
                    else
                    {
                        try
                        {
                            ProjectOptions.ValidateSvg(fc.Utf8Content, sourceInfoSprite);
                        }
                        catch
                        {
                            fileInfo.ReportDiag(true, -17, "Invalid or unusable svg " + name,
                                sourceInfoSprite.StartLine, sourceInfoSprite.StartCol,
                                sourceInfoSprite.EndLine, sourceInfoSprite.EndCol);
                        }
                    }
                }
            }

            if (Owner.ProjectOptions.SpriteGeneration)
            {
                var spriteHolder = Owner.ProjectOptions.SpriteGenerator;
                spriteHolder.Process(sourceInfo.Sprites);
            }
            else
            {
                sourceInfo.Sprites.ForEach(s =>
                {
                    if (s.Name == null || s.IsSvg())
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
                    {
                        fileInfo.ReportDiag(true, -8,
                            "Translation message must be compile time resolvable constant string, use f instead if intended", t.StartLine,
                            t.StartCol, t.EndLine, t.EndCol);
                        return;
                    }
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
                    {
                        fileInfo.ReportDiag(true, -8,
                            "Translation message must be compile time resolvable constant string, use f instead if intended", t.StartLine,
                            t.StartCol, t.EndLine, t.EndCol);
                        return;
                    }
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

    TsFileAdditionalInfo ReportDependency(TsFileAdditionalInfo owner, TsFileAdditionalInfo dep)
    {
        if (dep != null)
        {
            owner.ReportDependency(dep.Owner.FullPath);
        }

        return dep;
    }

    TsFileAdditionalInfo? AutodetectAndAddDependency(string depName, bool forceResource = false)
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

    readonly Dictionary<string, AstToplevel?> _parsedCache = new();

    public (string? fileName, AstToplevel? content) ResolveAndLoad(JsModule module)
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
            var parser = new Parser(new(), info.Output);
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
