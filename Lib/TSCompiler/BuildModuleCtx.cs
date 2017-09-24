using Lib.DiskCache;
using Lib.Utils;
using System;
using System.Linq;
using System.Collections.Generic;
using Lib.Composition;

namespace Lib.TSCompiler
{
    public class BuildModuleCtx : ITSCompilerCtx
    {
        public BuildModuleCtx()
        {
        }

        public BuildCtx _buildCtx;
        public TSProject _owner;
        public BuildResult _result;

        public void AddSource(TSFileAdditionalInfo file)
        {
            _result.Path2FileInfo[file.Owner.FullPath] = file;
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
            fileName = fileName.Substring(9);
            var fullPath = PathUtils.Join(_owner.Owner.FullPath, fileName);
            if (fullPath.EndsWith(".js"))
            {
                data = SourceMap.RemoveLinkToSourceMap(data);
                var sourceForJs = _result.Path2FileInfo[fullPath.Substring(0, fullPath.Length - ".js".Length) + ".ts"];
                _result.RecompiledLast.Add(sourceForJs);
                TrullyCompiledCount++;
                sourceForJs.Output = data;
                return;
            }
            if (!fullPath.EndsWith(".d.ts"))
            {
                throw new Exception("Unknown extension written by TS " + fullPath);
            }
            var dirPath = PathUtils.Parent(fullPath);
            var fileOnly = fullPath.Substring(dirPath.Length + 1);
            var dc = _owner.DiskCache.TryGetItem(dirPath) as IDirectoryCache;
            var wasChange = dc.WriteVirtualFile(fileOnly, data);
            var output = dc.TryGetChild(fileOnly) as IFileCache;
            var outputInfo = TSFileAdditionalInfo.Get(output, _owner.DiskCache);
            var source = _result.Path2FileInfo[fullPath.Substring(0, fullPath.Length - ".d.ts".Length)+".ts"];
            source.DtsLink = outputInfo;
            if (wasChange) ChangedDts = true;
        }

        static string[] ExtensionsToImport = new string[] { ".tsx", ".ts", ".d.ts", ".jsx", ".js" };
        internal OrderedHashSet<string> ToCheck;
        internal OrderedHashSet<string> ToCompile;
        internal uint CrawledCount;
        internal int TrullyCompiledCount;

        public bool ChangedDts { get; internal set; }

        static bool IsDts(string name)
        {
            if (name == null) return false;
            return name.EndsWith(".d.ts");
        }

        static bool IsTsOrTsx(string name)
        {
            if (name == null) return false;
            return name.EndsWith(".ts") || name.EndsWith(".tsx");
        }

        public string resolveLocalImport(string name, TSFileAdditionalInfo parentInfo)
        {
            var dirPath = PathUtils.Parent(name);
            var fileOnly = name.Substring(dirPath.Length + 1);
            var dc = _owner.DiskCache.TryGetItem(dirPath) as IDirectoryCache;
            if (dc == null || dc.IsInvalid)
                return null;
            _owner.DiskCache.UpdateIfNeeded(dc);
            var item = ExtensionsToImport.Select(ext => dc.TryGetChild(fileOnly + ext) as IFileCache).FirstOrDefault(i => i != null && !i.IsInvalid);
            if (item == null)
                return null;
            var itemInfo = TSFileAdditionalInfo.Get(item, _owner.DiskCache);
            parentInfo.ImportingLocal(itemInfo);
            if (IsDts(item.FullPath))
            {
                // implementation for .d.ts file currently needs to be added to build by b.asset("lib.js") and cannot have dependencies
            }
            else
            {
                itemInfo.Type = FileCompilationType.TypeScript;
                AddSource(itemInfo);
            }
            CheckAdd(item.FullPath);
            Crawl();
            if (itemInfo.DtsLink != null && !ToCompile.Contains(item.FullPath))
            {
                return itemInfo.DtsLink.Owner.FullPath;
            }
            return item.FullPath;
        }

        public string resolveModuleMain(string name, TSFileAdditionalInfo parentInfo)
        {
            var moduleInfo = TSProject.FindInfoForModule(_owner.Owner, _owner.DiskCache, name);
            if (moduleInfo == null) return null;
            moduleInfo.LoadProjectJson();
            parentInfo.ImportingModule(moduleInfo);
            var mainFile = PathUtils.Join(moduleInfo.Owner.FullPath, moduleInfo.MainFile);
            var item = _owner.DiskCache.TryGetItem(mainFile) as IFileCache;
            if (item == null || item.IsInvalid)
            {
                return null;
            }
            var itemInfo = TSFileAdditionalInfo.Get(item, _owner.DiskCache);
            itemInfo.ImportedAsModule = name;
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
            }
            itemInfo.Type = FileCompilationType.TypeScript;
            CheckAdd(item.FullPath);
            Crawl();
            if (itemInfo.DtsLink != null && !ToCompile.Contains(item.FullPath))
            {
                return itemInfo.DtsLink.Owner.FullPath;
            }
            return item.FullPath;
        }

        public void reportDiag(bool isError, int code, string text, string fileName, int startLine, int startCharacter, int endLine, int endCharacter)
        {
            var fc = _owner.DiskCache.TryGetItem(fileName) as IFileCache;
            if (fc == null)
            {
                throw new Exception("Cannot found " + fileName);
            }
            var fi = TSFileAdditionalInfo.Get(fc, _owner.DiskCache);
            Console.WriteLine((isError ? "Error" : "Warn") + " " + fileName + ":" + startLine + " TS" + code + " " + text);
            fi.ReportDiag(isError, code, text, startLine, startCharacter, endLine, endCharacter);
        }

        public bool CheckAdd(string fullNameWithExtension)
        {
            if (ToCheck.Contains(fullNameWithExtension))
                return false;
            ToCheck.Add(fullNameWithExtension);
            return true;
        }

        public void Crawl()
        {
            while (CrawledCount < ToCheck.Count)
            {
                var fileName = ToCheck[(int)CrawledCount];
                CrawledCount++;
                var fileCache = _owner.DiskCache.TryGetItem(fileName) as IFileCache;
                if (fileCache == null || fileCache.IsInvalid)
                {
                    continue; // skip missing files
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
                        case FileCompilationType.TypeScript:
                            if (fileAdditional.DtsLink != null)
                                fileAdditional.DtsLink.Owner.IsInvalid = true;
                            fileAdditional.DtsLink = null;
                            fileAdditional.Output = null;
                            fileAdditional.MapLink = null;
                            ToCompile.Add(fileName);
                            break;
                        case FileCompilationType.JavaScript:
                            fileAdditional.StartCompiling();
                            fileAdditional.Output = fileAdditional.Owner.Utf8Content;
                            _result.RecompiledLast.Add(fileAdditional);
                            TrullyCompiledCount++;
                            break;
                        case FileCompilationType.Resource:
                            fileAdditional.StartCompiling();
                            _result.RecompiledLast.Add(fileAdditional);
                            TrullyCompiledCount++;
                            break;
                        case FileCompilationType.Css:
                            fileAdditional.StartCompiling();
                            var cssProcessor = _buildCtx.CompilerPool.GetCss();
                            try
                            {
                                fileAdditional.Output = cssProcessor.ProcessCss(fileAdditional.Owner.Utf8Content, fileAdditional.Owner.FullPath, (string url, string from) =>
                                {
                                    var full = PathUtils.Join(from, url);
                                    var fullJustName = full.Split('?', '#')[0];
                                    fileAdditional.ImportingLocal(AutodetectAndAddDependency(fileAdditional, fullJustName));
                                    return full;
                                }).Result;
                            }
                            finally
                            {
                                _buildCtx.CompilerPool.ReleaseCss(cssProcessor);
                            }
                            _result.RecompiledLast.Add(fileAdditional);
                            TrullyCompiledCount++;
                            break;
                    }
                }
                else
                {
                    foreach (var localAdditional in fileAdditional.LocalImports)
                    {
                        var localName = localAdditional.Owner.FullPath;
                        if (localName.EndsWith(".d.ts")) continue; // we cannot handle change in .d.ts without source
                        CheckAdd(localName);
                    }
                    foreach (var moduleInfo in fileAdditional.ModuleImports)
                    {
                        moduleInfo.LoadProjectJson();
                        var mainFile = PathUtils.Join(moduleInfo.Owner.FullPath, moduleInfo.MainFile);
                        if (mainFile.EndsWith(".d.ts")) continue; // we cannot handle change in .d.ts without source
                        CheckAdd(mainFile);
                    }
                    AddDependenciesFromSourceInfo(fileAdditional);
                }
            }
        }

        public IDictionary<long, object[]> getPreEmitTransformations(string fileName)
        {
            var fc = _owner.DiskCache.TryGetItem(fileName) as IFileCache;
            if (fc == null) return null;
            var fai = TSFileAdditionalInfo.Get(fc, _owner.DiskCache);
            var sourceInfo = fai.SourceInfo;
            if (sourceInfo == null) return null;
            var res = new Dictionary<long, object[]>();
            sourceInfo.assets.ForEach(a =>
            {
                if (a.name == null) return;
                res[a.nodeId] = new object[] { 0, PathUtils.Subtract(a.name, _owner.Owner.FullPath) };
            });
            sourceInfo.sprites.ForEach(s =>
            {
                if (s.name == null) return;
                res[s.nodeId] = new object[] { 0, PathUtils.Subtract(s.name, _owner.Owner.FullPath) };
            });
            if (res.Count == 0) return null;
            return res;
        }

        public void AddDependenciesFromSourceInfo(TSFileAdditionalInfo fileInfo)
        {
            var sourceInfo = fileInfo.SourceInfo;
            if (sourceInfo == null) return;
            sourceInfo.assets.ForEach(a =>
            {
                if (a.name == null) return;
                var assetName = a.name;
                AutodetectAndAddDependency(fileInfo, assetName);
            });
            sourceInfo.sprites.ForEach(s =>
            {
                if (s.name == null) return;
                var assetName = s.name;
                AutodetectAndAddDependency(fileInfo, assetName);
            });
        }

        TSFileAdditionalInfo AutodetectAndAddDependency(TSFileAdditionalInfo fileInfo, string depName)
        {
            var extension = PathUtils.GetExtension(depName);
            var depFile = fileInfo.DiskCache.TryGetItem(depName) as IFileCache;
            if (depFile == null)
            {
                // TODO: show error about not found
                return null;
            }
            var assetFileInfo = TSFileAdditionalInfo.Get(depFile, fileInfo.DiskCache);
            switch (extension)
            {
                case "css":
                    assetFileInfo.Type = FileCompilationType.Css;
                    CheckAdd(depName);
                    break;
                case "js":
                    assetFileInfo.Type = FileCompilationType.JavaScript;
                    CheckAdd(depName);
                    break;
                default:
                    assetFileInfo.Type = FileCompilationType.Resource;
                    CheckAdd(depName);
                    break;
            }
            return assetFileInfo;
        }
    }
}
