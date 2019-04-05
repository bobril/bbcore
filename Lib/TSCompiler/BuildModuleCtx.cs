using Lib.DiskCache;
using Lib.Utils;
using System;
using System.Linq;
using System.Collections.Generic;
using Lib.Composition;
using System.Text.RegularExpressions;

namespace Lib.TSCompiler
{
    public class BuildModuleCtx : ITSCompilerCtx
    {
        public BuildCtx _buildCtx;
        public TSProject _owner;
        public BuildResult _result;
        public int OutputedJsFiles;
        public int OutputedDtsFiles;

        public void AddSource(TSFileAdditionalInfo file)
        {
            _result.Path2FileInfo[file.Owner.FullPath] = file;
            if (file.MyProject != null && file.ImportedAsModule != null)
            {
                _result.Modules.TryAdd(file.ImportedAsModule, file.MyProject);
            }
        }

        public void UpdateCacheIds()
        {
            foreach (var fileInfo in _result.RecompiledLast)
            {
                fileInfo.RememberLastCompilationCacheIds();
            }
        }

        public void writeFile(string fileName, string data)
        {
            if (fileName.EndsWith(".js.map"))
            {
                var relativeTo = PathUtils.Parent(PathUtils.Join(_owner.Owner.FullPath, fileName));
                var sourceMap = SourceMap.Parse(data, relativeTo);
                var sourceFullPath = sourceMap.sources[0];
                var sourceForMap = _result.Path2FileInfo[sourceFullPath];
                sourceForMap.MapLink = sourceMap;
                return;
            }

            if (!fileName.StartsWith("_virtual/"))
                throw new Exception("writeFile does not start with _virtual");
            var fullPathWithVirtual = PathUtils.Join(_owner.ProjectOptions.CurrentBuildCommonSourceDirectory, fileName);
            fileName = fileName.Substring(9);
            var fullPath = PathUtils.Join(_owner.ProjectOptions.CurrentBuildCommonSourceDirectory, fileName);
            if (fileName.EndsWith(".json"))
            {
                _result.Path2FileInfo.TryGetValue(fullPath, out var sourceForJs);
                _result.RecompiledLast.Add(sourceForJs);
                return;
            }

            if (fullPath.EndsWith(".js"))
            {
                OutputedJsFiles++;
                data = SourceMap.RemoveLinkToSourceMap(data);
                var sourceName = fullPath.Substring(0, fullPath.Length - ".js".Length) + ".ts";
                TSFileAdditionalInfo sourceForJs = null;
                if (!_result.Path2FileInfo.TryGetValue(sourceName, out sourceForJs))
                    _result.Path2FileInfo.TryGetValue(sourceName + "x", out sourceForJs);
                if (sourceForJs == null)
                {
                    if (!_result.Path2FileInfo.TryGetValue(fullPath, out sourceForJs))
                        _result.Path2FileInfo.TryGetValue(fullPath + "x", out sourceForJs);
                }

                sourceForJs.Output = data;
                _result.RecompiledLast.Add(sourceForJs);
                return;
            }

            if (!fullPath.EndsWith(".d.ts"))
            {
                throw new Exception("Unknown extension written by TS " + fullPath);
            }

            OutputedDtsFiles++;
            data = new Regex("\\/\\/\\/ *<reference path=\\\"(.+)\\\" *\\/>").Replace(data, (m) =>
            {
                var origPath = m.Groups[1].Value;
                var newPath = PathUtils.Subtract(PathUtils.Join(PathUtils.Parent(fullPathWithVirtual), origPath),
                    PathUtils.Parent(fullPath));
                return "/// <reference path=\"" + newPath + "\" />";
            });
            var dirPath = PathUtils.Parent(fullPath);
            var fileOnly = fullPath.Substring(dirPath.Length + 1);
            var dc = _owner.DiskCache.TryGetItem(dirPath) as IDirectoryCache;
            var wasChange = dc.WriteVirtualFile(fileOnly, data);
            var output = dc.TryGetChild(fileOnly) as IFileCache;
            var outputInfo = TSFileAdditionalInfo.Get(output, _owner.DiskCache);
            var sourceName2 = fullPath.Substring(0, fullPath.Length - ".d.ts".Length) + ".ts";
            TSFileAdditionalInfo source = null;
            if (!_result.Path2FileInfo.TryGetValue(sourceName2, out source))
                _result.Path2FileInfo.TryGetValue(sourceName2 + "x", out source);
            source.DtsLink = outputInfo;
            outputInfo.MyProject = source.MyProject;
            if (wasChange)
                ChangedDts = true;
        }

        public string resolveLocalImport(string name, TSFileAdditionalInfo parentInfo)
        {
            return resolveLocalImport(name, parentInfo, null);
        }

        static readonly string[] ExtensionsToImport = {".tsx", ".ts", ".d.ts", ".jsx", ".js"};
        internal OrderedHashSet<string> ToCheck;
        internal OrderedHashSet<string> ToCompile;
        internal OrderedHashSet<string> ToCompileDts;
        internal uint CrawledCount;

        public bool ChangedDts { get; internal set; }

        static bool IsDts(string name)
        {
            if (name == null)
                return false;
            return name.EndsWith(".d.ts");
        }

        static bool IsTsOrTsx(string name)
        {
            if (name == null)
                return false;
            return name.EndsWith(".ts") || name.EndsWith(".tsx");
        }

        public readonly Dictionary<string, string> LocalResolveCache = new Dictionary<string, string>();

        public string resolveLocalImport(string name, TSFileAdditionalInfo parentInfo, TSProject moduleInfo)
        {
            var dirPath = PathUtils.Parent(name);
            var fileOnly = name.Substring(dirPath.Length + 1);
            var dc = _owner.DiskCache.TryGetItem(dirPath) as IDirectoryCache;
            if (dc == null || dc.IsInvalid)
                return null;
            var isJson = false;
            IFileCache item = null;
            if (fileOnly.EndsWith(".json"))
            {
                item = dc.TryGetChild(fileOnly) as IFileCache;
                if (item != null) isJson = true;
            }

            if (item == null)
                item = ExtensionsToImport.Select(ext => dc.TryGetChild(fileOnly + ext) as IFileCache)
                    .FirstOrDefault(i => i != null && !i.IsInvalid);

            if (item == null)
                return null;
            if (item.FullPath.Substring(0, name.Length) != name)
            {
                parentInfo.ReportDiag(false, -1,
                    "Local import has wrong casing '" + name + "' on disk '" + item.FullPath + "'", 0, 0, 0, 0);
            }

            var itemInfo = TSFileAdditionalInfo.Get(item, _owner.DiskCache);
            parentInfo.ImportingLocal(itemInfo);
            itemInfo.MyProject = moduleInfo ?? parentInfo.MyProject;
            if (IsDts(item.FullPath))
            {
                if (dc.TryGetChild(fileOnly + ".js") is IFileCache jsItem)
                {
                    var jsItemInfo = TSFileAdditionalInfo.Get(jsItem, _owner.DiskCache);
                    jsItemInfo.Type = FileCompilationType.JavaScript;
                    parentInfo.ImportingLocal(jsItemInfo);
                    CheckAdd(jsItem.FullPath);
                }

                // implementation for .d.ts file does not have same name, it needs to be added to build by b.asset("lib.js") and cannot have dependencies
            }
            else
            {
                itemInfo.Type = isJson ? FileCompilationType.Json : FileCompilationType.TypeScript;
                AddSource(itemInfo);
            }

            if (LocalResolveCache.TryGetValue(name, out var res))
            {
                return res;
            }

            CheckAdd(item.FullPath);
            TryToResolveFromBuildCache(itemInfo);

            if (itemInfo.DtsLink != null && !ToCompile.Contains(item.FullPath) && !itemInfo.NeedsCompilation())
            {
                res = itemInfo.DtsLink.Owner.FullPath;
            }
            else
            {
                res = item.FullPath;
            }

            LocalResolveCache.Add(name, res);
            return res;
        }

        void TryToResolveFromBuildCache(TSFileAdditionalInfo itemInfo)
        {
            itemInfo.TakenFromBuildCache = false;
            var bc = _owner.ProjectOptions.BuildCache;
            if (bc.IsEnabled)
            {
                var hashOfContent = itemInfo.Owner.HashOfContent;
                var confId = _owner.ProjectOptions.ConfigurationBuildCacheId;
                var fbc = (itemInfo.BuildCacheHash == hashOfContent && itemInfo.BuildCacheConfId == confId)
                    ? itemInfo.BuildCacheValue
                    : bc.FindTSFileBuildCache(hashOfContent, confId);
                itemInfo.BuildCacheHash = hashOfContent;
                itemInfo.BuildCacheConfId = confId;
                itemInfo.BuildCacheValue = fbc;
                if (fbc != null)
                {
                    if ((fbc.LocalImports?.Count ?? 0) == 0 && (fbc.ModuleImports?.Count ?? 0) == 0)
                    {
                        itemInfo.StartCompiling();
                        itemInfo.Output = fbc.JsOutput;
                        itemInfo.MapLink = fbc.MapLink;
                        var fullPath = PathUtils.ChangeExtension(itemInfo.Owner.FullPath, "d.ts");
                        var dirPath = PathUtils.Parent(fullPath);
                        var fileOnly = fullPath.Substring(dirPath.Length + 1);
                        var dc = _owner.DiskCache.TryGetItem(dirPath) as IDirectoryCache;
                        bool wasChange = false;
                        if (fbc.DtsOutput != null)
                        {
                            wasChange = dc.WriteVirtualFile(fileOnly, fbc.DtsOutput);
                            var output = dc.TryGetChild(fileOnly) as IFileCache;
                            itemInfo.DtsLink = TSFileAdditionalInfo.Get(output, _owner.DiskCache);
                            itemInfo.DtsLink.MyProject = itemInfo.MyProject;
                        }
                        else
                        {
                            itemInfo.DtsLink = null;
                        }

                        if (wasChange)
                        {
                            ChangedDts = true;
                        }

                        itemInfo.RememberLastCompilationCacheIds();
                        itemInfo.TakenFromBuildCache = true;
                    }
                }
            }
        }

        public string resolveModuleMain(string name, TSFileAdditionalInfo parentInfo)
        {
            if (!_owner.ProjectOptions.AllowModuleDeepImport)
            {
                if (!parentInfo.Owner.Name.EndsWith(".d.ts") && (name.Contains('/') || name.Contains('\\')))
                {
                    parentInfo.ReportDiag(true, -10, "Absolute import '" + name + "' must be just simple module name", 0, 0, 0, 0);
                    return null;
                }
            }

            var mname = PathUtils.EnumParts(name).First().name;
            var moduleInfo =
                TSProject.FindInfoForModule(_owner.Owner, parentInfo.Owner.Parent, _owner.DiskCache, _owner.Logger, mname, out var diskName);
            if (moduleInfo == null)
                return null;
            if (mname != diskName)
            {
                parentInfo.ReportDiag(false, -2,
                    "Module import has wrong casing '" + mname + "' on disk '" + diskName + "'", 0, 0, 0, 0);
            }

            moduleInfo.LoadProjectJson(true);
            if (mname.Length != name.Length)
            {
                return resolveLocalImport(PathUtils.Join(moduleInfo.Owner.FullPath, name.Substring(mname.Length + 1)),
                    parentInfo, moduleInfo);
            }
            parentInfo.ImportingModule(moduleInfo);
            var mainFile = PathUtils.Join(moduleInfo.Owner.FullPath, moduleInfo.MainFile);
            var item = _owner.DiskCache.TryGetItem(mainFile) as IFileCache;
            if (item == null || item.IsInvalid)
            {
                return null;
            }

            var itemInfo = TSFileAdditionalInfo.Get(item, _owner.DiskCache);
            moduleInfo.MainFileInfo = itemInfo;
            itemInfo.ImportedAsModule = name;
            itemInfo.MyProject = moduleInfo;
            var parentProject = parentInfo.MyProject;
            if (parentProject != null && parentProject.IsRootProject &&
                ((parentProject.Dependencies == null || !parentProject.Dependencies.Contains(name)) &&
                 (parentProject.DevDependencies == null || !parentProject.DevDependencies.Contains(name))))
            {
                parentInfo.ReportDiag(false, -12,
                    "Importing module " + name + " without being in package.json as dependency", 0, 0, 0, 0);
            }

            if (moduleInfo.ProjectOptions?.ObsoleteMessage != null)
            {
                if (!PragmaParser.ParseIgnoreImportingObsolete(parentInfo.Owner.Utf8Content).Contains(name))
                {
                    parentInfo.ReportDiag(false, -14,
                        "Importing obsolete module: " + moduleInfo.ProjectOptions?.ObsoleteMessage, 0, 0, 0, 0);
                }
            }

            AddSource(itemInfo);
            if (!IsTsOrTsx(mainFile))
            {
                itemInfo.Type = FileCompilationType.JavaScript;
                CheckAdd(mainFile);
                if (moduleInfo.TypesMainFile != null)
                {
                    var dtsPath = PathUtils.Join(moduleInfo.Owner.FullPath, moduleInfo.TypesMainFile);
                    item = _owner.DiskCache.TryGetItem(dtsPath) as IFileCache;
                    itemInfo.DtsLink = TSFileAdditionalInfo.Get(item, _owner.DiskCache);
                    if (item != null && !item.IsInvalid)
                    {
                        return dtsPath;
                    }
                }

                return null;
            }

            itemInfo.Type = FileCompilationType.TypeScript;
            CheckAdd(item.FullPath);
            TryToResolveFromBuildCache(itemInfo);
            if (itemInfo.DtsLink != null && !ToCompile.Contains(item.FullPath) && !itemInfo.NeedsCompilation())
            {
                return itemInfo.DtsLink.Owner.FullPath;
            }

            return item.FullPath;
        }

        public void reportDiag(bool isError, int code, string text, string fileName, int startLine, int startCharacter,
            int endLine, int endCharacter)
        {
            var fc = _owner.DiskCache.TryGetItem(fileName) as IFileCache;
            if (fc == null)
            {
                throw new Exception("Cannot found " + fileName);
            }

            var fi = TSFileAdditionalInfo.Get(fc, _owner.DiskCache);
            fi.ReportDiag(isError, code, text, startLine, startCharacter, endLine, endCharacter);
        }

        public bool CheckAdd(string fullNameWithExtension)
        {
            if (ToCheck.Contains(fullNameWithExtension))
                return false;
            ToCheck.Add(fullNameWithExtension);
            return true;
        }

        public string ExpandHtmlHead(string htmlHead)
        {
            return new Regex("<<[^>]+>>").Replace(htmlHead,
                (Match m) =>
                {
                    return AutodetectAndAddDependency(
                        PathUtils.Join(_owner.Owner.FullPath, m.Value.Substring(2, m.Length - 4)),
                        _owner.Owner.TryGetChild("package.json") as IFileCache).OutputUrl;
                });
        }

        public void Crawl()
        {
            while (CrawledCount < ToCheck.Count)
            {
                var fileName = ToCheck[(int) CrawledCount];
                CrawledCount++;
                var fileCache = _owner.DiskCache.TryGetItem(fileName) as IFileCache;
                if (fileCache == null || fileCache.IsInvalid)
                {
                    if (_buildCtx.Verbose)
                        Console.WriteLine("Crawl skipping missing file " + fileName);
                    continue;
                }

                var fileAdditional = TSFileAdditionalInfo.Get(fileCache, _owner.DiskCache);
                AddSource(fileAdditional);
                if (fileAdditional.Type == FileCompilationType.Unknown)
                {
                    fileAdditional.Type = FileCompilationType.TypeScript;
                }

                if (fileAdditional.NeedsCompilation())
                {
                    switch (fileAdditional.Type)
                    {
                        case FileCompilationType.Json:
                            if (fileAdditional.MyProject == null)
                            {
                                fileAdditional.MyProject = _owner;
                            }

                            fileAdditional.Output = null;
                            fileAdditional.MapLink = null;
                            break;
                        case FileCompilationType.TypeScript:
                            if (fileAdditional.MyProject == null)
                            {
                                fileAdditional.MyProject = _owner;
                            }

                            fileAdditional.Output = null;
                            fileAdditional.MapLink = null;
                            // d.ts files are compiled always but they don't have any output so needs to be in separate set
                            if (fileName.EndsWith(".d.ts"))
                                ToCompileDts.Add(fileName);
                            else
                                ToCompile.Add(fileName);
                            break;
                        case FileCompilationType.JavaScript:
                        case FileCompilationType.JavaScriptAsset:
                            fileAdditional.StartCompiling();
                            fileAdditional.Output = fileAdditional.Owner.Utf8Content;
                            fileAdditional.MapLink =
                                SourceMap.Identity(fileAdditional.Output, fileAdditional.Owner.FullPath);
                            _result.RecompiledLast.Add(fileAdditional);
                            break;
                        case FileCompilationType.Resource:
                            fileAdditional.StartCompiling();
                            _result.RecompiledLast.Add(fileAdditional);
                            break;
                        case FileCompilationType.Css:
                            fileAdditional.StartCompiling();
                            if (!_owner.ProjectOptions.BundleCss)
                            {
                                var cssProcessor = _buildCtx.CompilerPool.GetCss();
                                try
                                {
                                    fileAdditional.Output = cssProcessor.ProcessCss(fileAdditional.Owner.Utf8Content,
                                        fileAdditional.Owner.FullPath, (string url, string from) =>
                                        {
                                            var full = PathUtils.Join(from, url);
                                            var fullJustName = full.Split('?', '#')[0];
                                            var fileAdditionalInfo =
                                                AutodetectAndAddDependency(fullJustName, fileAdditional.Owner);
                                            fileAdditional.ImportingLocal(fileAdditionalInfo);
                                            return PathUtils.Subtract(fileAdditionalInfo.OutputUrl,
                                                       PathUtils.Parent(fileAdditional.OutputUrl)) +
                                                   full.Substring(fullJustName.Length);
                                        }).Result;
                                }
                                finally
                                {
                                    _buildCtx.CompilerPool.ReleaseCss(cssProcessor);
                                }

                                _result.RecompiledLast.Add(fileAdditional);
                            }

                            break;
                    }
                }
                else
                {
                    foreach (var localAdditional in fileAdditional.LocalImports)
                    {
                        var localName = localAdditional.Owner.FullPath;
                        if (localName.EndsWith(".d.ts"))
                            continue; // we cannot handle change in .d.ts without source
                        CheckAdd(localName);
                    }

                    foreach (var moduleInfo in fileAdditional.ModuleImports)
                    {
                        moduleInfo.LoadProjectJson(true);
                        var mainFile = PathUtils.Join(moduleInfo.Owner.FullPath, moduleInfo.MainFile);
                        if (mainFile.EndsWith(".d.ts"))
                            continue; // we cannot handle change in .d.ts without source
                        CheckAdd(mainFile);
                    }

                    RefreshDependenciesFromSourceInfo(fileAdditional);
                }
            }
        }

        public string ToOutputUrl(string fileName)
        {
            var assetFileInfo =
                TSFileAdditionalInfo.Get(_owner.DiskCache.TryGetItem(fileName) as IFileCache, _owner.DiskCache);
            if (assetFileInfo == null)
                return fileName;
            if (_owner.ProjectOptions.BundleCss && assetFileInfo.Type == FileCompilationType.Css)
            {
                return fileName;
            }

            if (assetFileInfo.OutputUrl == null)
                assetFileInfo.OutputUrl =
                    _owner.ProjectOptions.AllocateName(PathUtils.Subtract(fileName, _owner.Owner.FullPath));
            return assetFileInfo.OutputUrl;
        }

        public IDictionary<long, object[]> getPreEmitTransformations(string fileName)
        {
            var fc = _owner.DiskCache.TryGetItem(fileName) as IFileCache;
            if (fc == null)
                return null;
            var fai = TSFileAdditionalInfo.Get(fc, _owner.DiskCache);
            var sourceInfo = fai.SourceInfo;
            if (sourceInfo == null)
                return null;
            var res = new Dictionary<long, object[]>();
            sourceInfo.assets.ForEach(a =>
            {
                if (a.name == null)
                    return;
                res[a.nodeId] = new object[] {0, ToOutputUrl(a.name)};
            });
            if (_owner.ProjectOptions.SpriteGeneration)
            {
                var spriteHolder = _owner.ProjectOptions.SpriteGenerator;
                spriteHolder.Retrieve(sourceInfo.sprites);
                sourceInfo.sprites.ForEach(s =>
                {
                    if (s.name == null)
                        return;
                    if (s.hasColor == true && s.color == null)
                    {
                        res[s.nodeId] = new object[]
                        {
                            5,
                            2, 1, s.owidth, 5,
                            2, 2, s.oheight, 5,
                            2, 3, s.ox, 5,
                            2, 4, s.oy, 5,
                            4, "spritebc"
                        };
                    }
                    else
                    {
                        res[s.nodeId] = new object[]
                        {
                            2, 0, s.owidth, 4,
                            2, 1, s.oheight, 4,
                            2, 2, s.ox, 4,
                            2, 3, s.oy, 4,
                            4, "spriteb"
                        };
                    }
                });
            }
            else
            {
                sourceInfo.sprites.ForEach(s =>
                {
                    if (s.name == null)
                        return;
                    res[s.nodeId] = new object[] {0, ToOutputUrl(s.name)};
                });
            }

            var trdb = _owner.ProjectOptions.TranslationDb;
            if (trdb != null)
            {
                sourceInfo.translations.ForEach(t =>
                {
                    if (t.message == null)
                        return;
                    if (t.withParams)
                    {
                        var err = trdb.CheckMessage(t.message, t.knownParams);
                        if (err != null)
                        {
                            fai.ReportDiag(false, -7,
                                "Problem with translation message \"" + t.message + "\" " + err.ToString(), 0, 0, 0, 0);
                        }
                    }

                    if (t.justFormat)
                        return;
                    var id = trdb.AddToDB(t.message, t.hint, t.withParams);
                    var finalId = trdb.MapId(id);
                    res[t.nodeId] = new object[] {2, 0, finalId, 1 + (t.withParams ? 1 : 0)};
                });
            }

            var styleDefNaming = _owner.ProjectOptions.StyleDefNaming;
            var styleDefPrefix = _owner.ProjectOptions.PrefixStyleNames;
            sourceInfo.styleDefs.ForEach(s =>
            {
                var skipEx = s.isEx ? 1 : 0;
                if (s.userNamed)
                {
                    if (styleDefNaming == StyleDefNamingStyle.AddNames ||
                        styleDefNaming == StyleDefNamingStyle.PreserveNames)
                    {
                        if (styleDefPrefix.Length > 0)
                        {
                            if (s.name != null)
                            {
                                res[s.nodeId] = new object[] {2, 2 + skipEx, styleDefPrefix + s.name, 3 + skipEx};
                            }
                            else
                            {
                                res[s.nodeId] = new object[] {3, 2 + skipEx, styleDefPrefix, 3 + skipEx};
                            }
                        }
                    }
                    else
                    {
                        res[s.nodeId] = new object[] {1, 2 + skipEx};
                    }
                }
                else
                {
                    if (styleDefNaming == StyleDefNamingStyle.AddNames && s.name != null)
                    {
                        // TODO: heuristicaly improve s.name by filename
                        res[s.nodeId] = new object[] {2, 2 + skipEx, styleDefPrefix + s.name, 3 + skipEx};
                    }
                }
            });
            if (res.Count == 0)
                return null;
            return res;
        }

        public void RefreshDependenciesFromSourceInfo(TSFileAdditionalInfo fileInfo)
        {
            var sourceInfo = fileInfo.SourceInfo;
            if (sourceInfo == null)
                return;
            sourceInfo.assets.ForEach(a =>
            {
                if (a.name == null)
                    return;
                CheckAdd(a.name);
            });
            if (!_owner.ProjectOptions.SpriteGeneration)
            {
                sourceInfo.sprites.ForEach(s =>
                {
                    if (s.name == null)
                        return;
                    CheckAdd(s.name);
                });
            }
        }

        public void AddDependenciesFromSourceInfo(TSFileAdditionalInfo fileInfo)
        {
            var sourceInfo = fileInfo.SourceInfo;
            if (sourceInfo == null)
                return;
            sourceInfo.assets.ForEach(a =>
            {
                if (a.name == null)
                    return;
                var assetName = a.name;
                AutodetectAndAddDependency(assetName, fileInfo.Owner);
            });
            if (_owner.ProjectOptions.SpriteGeneration)
            {
                var spriteHolder = _owner.ProjectOptions.SpriteGenerator;
                spriteHolder.Process(sourceInfo.sprites);
            }
            else
            {
                sourceInfo.sprites.ForEach(s =>
                {
                    if (s.name == null)
                        return;
                    var assetName = s.name;
                    AutodetectAndAddDependency(assetName, fileInfo.Owner);
                });
            }
        }

        public static TSFileAdditionalInfo AutodetectAndAddDependencyCore(ProjectOptions projectOptions, string depName,
            IFileCache usedFrom)
        {
            var dc = projectOptions.Owner.DiskCache;
            var extension = PathUtils.GetExtension(depName);
            var depFile = dc.TryGetItem(depName) as IFileCache;
            if (depFile == null)
            {
                if (usedFrom != null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("In " + usedFrom.FullPath + " missing dependency " + depName);
                    Console.ForegroundColor = ConsoleColor.Gray;
                    TSFileAdditionalInfo.Get(usedFrom, dc)
                        .ReportDiag(true, -3, "Missing dependency " + depName, 0, 0, 0, 0);
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Somethere missing dependency " + depName);
                    Console.ForegroundColor = ConsoleColor.Gray;
                }

                return null;
            }

            var assetFileInfo = TSFileAdditionalInfo.Get(depFile, dc);
            if (projectOptions.BundleCss && extension == "css")
            {
                assetFileInfo.Type = FileCompilationType.Css;
                return assetFileInfo;
            }

            if (assetFileInfo.OutputUrl == null)
                assetFileInfo.OutputUrl =
                    projectOptions.AllocateName(PathUtils.Subtract(depFile.FullPath,
                        projectOptions.Owner.Owner.FullPath));
            switch (extension)
            {
                case "css":
                    assetFileInfo.Type = FileCompilationType.Css;
                    break;
                case "js":
                    assetFileInfo.Type = FileCompilationType.JavaScriptAsset;
                    break;
                default:
                    assetFileInfo.Type = FileCompilationType.Resource;
                    break;
            }

            return assetFileInfo;
        }

        TSFileAdditionalInfo AutodetectAndAddDependency(string depName, IFileCache usedFrom)
        {
            var fai = AutodetectAndAddDependencyCore(_owner.ProjectOptions, depName, usedFrom);
            if (fai != null)
                CheckAdd(depName);
            return fai;
        }
    }
}
