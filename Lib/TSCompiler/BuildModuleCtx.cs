using Lib.DiskCache;
using Lib.Utils;
using System;
using System.Linq;

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
            _result.WithoutExtension2Source[PathUtils.WithoutExtension(file.Owner.FullPath)] = file;
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
                var sourceFullPath = PathUtils.WithoutExtension(sourceMap.sources[0]);
                var sourceForMap = _result.WithoutExtension2Source[sourceFullPath];
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
                var sourceForJs = _result.WithoutExtension2Source[fullPath.Substring(0, fullPath.Length - ".js".Length)];
                _result.RecompiledLast.Add(sourceForJs);
                TrullyCompiledCount++;
                sourceForJs.JsOutput = data;
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
            var source = _result.WithoutExtension2Source[fullPath.Substring(0, fullPath.Length - ".d.ts".Length)];
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
                if (moduleInfo.TypesMainFile != null)
                {
                    var dtsPath = PathUtils.Join(moduleInfo.Owner.FullPath, moduleInfo.TypesMainFile);
                    item = _owner.DiskCache.TryGetItem(dtsPath) as IFileCache;
                    if (item != null && !item.IsInvalid)
                    {
                        return dtsPath;
                    }
                }
            }
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
                if (fileAdditional.NeedsCompilation())
                {
                    if (fileAdditional.DtsLink != null)
                        fileAdditional.DtsLink.Owner.IsInvalid = true;
                    fileAdditional.DtsLink = null;
                    fileAdditional.JsOutput = null;
                    fileAdditional.MapLink = null;
                    ToCompile.Add(fileName);
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
                }
            }
        }
    }
}
