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
            var fullPathWithVirtual = PathUtils.Join(_owner.Owner.FullPath, fileName);
            fileName = fileName.Substring(9);
            var fullPath = PathUtils.Join(_owner.Owner.FullPath, fileName);
            if (fullPath.EndsWith(".js"))
            {
                data = SourceMap.RemoveLinkToSourceMap(data);
                var sourceName = fullPath.Substring(0, fullPath.Length - ".js".Length) + ".ts";
                TSFileAdditionalInfo sourceForJs = null;
                if (!_result.Path2FileInfo.TryGetValue(sourceName, out sourceForJs))
                    _result.Path2FileInfo.TryGetValue(sourceName + "x", out sourceForJs);
                sourceForJs.Output = data;
                _result.RecompiledLast.Add(sourceForJs);
                TrullyCompiledCount++;
                return;
            }
            if (!fullPath.EndsWith(".d.ts"))
            {
                throw new Exception("Unknown extension written by TS " + fullPath);
            }
            data = new Regex("\\/\\/\\/ <reference path=\\\"(.+)\\\" \\/>").Replace(data, (m) =>
            {
                var origPath = m.Groups[1].Value;
                var newPath = PathUtils.Subtract(PathUtils.Join(PathUtils.Parent(fullPathWithVirtual), origPath), PathUtils.Parent(fullPath));
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
            if (wasChange)
                ChangedDts = true;
        }

        static string[] ExtensionsToImport = new string[] { ".tsx", ".ts", ".d.ts", ".jsx", ".js" };
        internal OrderedHashSet<string> ToCheck;
        internal OrderedHashSet<string> ToCompile;
        internal OrderedHashSet<string> ToCompileDts;
        internal uint CrawledCount;
        internal int TrullyCompiledCount;

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

        public string resolveLocalImport(string name, TSFileAdditionalInfo parentInfo)
        {
            var dirPath = PathUtils.Parent(name);
            var fileOnly = name.Substring(dirPath.Length + 1);
            var dc = _owner.DiskCache.TryGetItem(dirPath) as IDirectoryCache;
            if (dc == null || dc.IsInvalid)
                return null;
            var item = ExtensionsToImport.Select(ext => dc.TryGetChild(fileOnly + ext) as IFileCache).FirstOrDefault(i => i != null && !i.IsInvalid);
            if (item == null)
                return null;
            if (item.FullPath.Substring(0, name.Length) != name)
            {
                parentInfo.ReportDiag(false, 1, "Local import has wrong casing '" + name + "' on disk '" + item.FullPath + "'", 0, 0, 0, 0);
            }
            var itemInfo = TSFileAdditionalInfo.Get(item, _owner.DiskCache);
            parentInfo.ImportingLocal(itemInfo);
            if (IsDts(item.FullPath))
            {
                var jsItem = dc.TryGetChild(fileOnly + ".js") as IFileCache;
                if (jsItem != null)
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
            var moduleInfo = TSProject.FindInfoForModule(_owner.Owner, _owner.DiskCache, name, out var diskName);
            if (moduleInfo == null)
                return null;
            if (name != diskName)
            {
                parentInfo.ReportDiag(false, 2, "Module import has wrong casing '" + name + "' on disk '" + diskName + "'", 0, 0, 0, 0);
            }
            moduleInfo.LoadProjectJson(true);
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
                return null;
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
            Console.ForegroundColor = isError ? ConsoleColor.Red : ConsoleColor.Yellow;
            Console.WriteLine(PathUtils.Subtract(fileName, _owner.Owner.FullPath) + "(" + (startLine + 1) + "," + (startCharacter + 1) + "): " + (isError ? "error" : "warning") + " TS" + code + ": " + text);
            Console.ForegroundColor = ConsoleColor.Gray;
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
            return new Regex("<<[^>]+>>").Replace(htmlHead, (Match m) =>
            {
                return ShortenPathAddVersionDir(AutodetectAndAddDependency(_owner.DiskCache, PathUtils.Join(_owner.Owner.FullPath, m.Value.Substring(2, m.Length - 4)), _owner.Owner.TryGetChild("package.json") as IFileCache).Owner.FullPath);
            });
        }

        public string ShortenPathAddVersionDir(string fullPath)
        {
            // TODO: finish this ...
            return PathUtils.Subtract(fullPath, _owner.Owner.FullPath);
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
                        case FileCompilationType.TypeScript:
                            if (fileAdditional.DtsLink != null)
                                fileAdditional.DtsLink.Owner.IsInvalid = true;
                            fileAdditional.DtsLink = null;
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
                            fileAdditional.MapLink = SourceMap.Identity(fileAdditional.Output, fileAdditional.Owner.FullPath);
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
                                    fileAdditional.ImportingLocal(AutodetectAndAddDependency(fileAdditional.DiskCache, fullJustName, fileAdditional.Owner));
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
                    AddDependenciesFromSourceInfo(fileAdditional);
                }
            }
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
                res[a.nodeId] = new object[] { 0, PathUtils.Subtract(a.name, _owner.Owner.FullPath) };
            });
            sourceInfo.sprites.ForEach(s =>
            {
                if (s.name == null)
                    return;
                res[s.nodeId] = new object[] { 0, PathUtils.Subtract(s.name, _owner.Owner.FullPath) };
            });
            var trdb = _owner.ProjectOptions.TranslationDb;
            if (trdb != null)
            {
                sourceInfo.translations.ForEach(t =>
                {
                    if (t.justFormat)
                        return;
                    if (t.message == null)
                        return;
                    var id = trdb.AddToDB(t.message, t.hint, t.withParams);
                    var finalId = trdb.MapId(id);
                    res[t.nodeId] = new object[] { 2, 0, finalId, 1 + (t.withParams ? 1 : 0) };
                });
            }
            var styleDefNaming = _owner.ProjectOptions.StyleDefNaming;
            var styleDefPrefix = _owner.ProjectOptions.PrefixStyleNames;
            sourceInfo.styleDefs.ForEach(s =>
            {
                var skipEx = s.isEx ? 1 : 0;
                if (s.userNamed)
                {
                    if (styleDefNaming == StyleDefNamingStyle.AddNames || styleDefNaming == StyleDefNamingStyle.PreserveNames)
                    {
                        if (styleDefPrefix.Length > 0)
                        {
                            if (s.name != null)
                            {
                                res[s.nodeId] = new object[] { 2, 2 + skipEx, styleDefPrefix + s.name, 3 + skipEx };
                            }
                            else
                            {
                                res[s.nodeId] = new object[] { 3, 2 + skipEx, styleDefPrefix, 3 + skipEx };
                            }
                        }
                    }
                    else
                    {
                        res[s.nodeId] = new object[] { 1, 2 + skipEx };
                    }
                }
                else
                {
                    if (styleDefNaming == StyleDefNamingStyle.AddNames && s.name != null)
                    {
                        // TODO: heuristicaly improve s.name by filename
                        res[s.nodeId] = new object[] { 2, 2 + skipEx, styleDefPrefix + s.name, 3 + skipEx };
                    }
                }
            });
            if (res.Count == 0)
                return null;
            return res;
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
                AutodetectAndAddDependency(fileInfo.DiskCache, assetName, fileInfo.Owner);
            });
            sourceInfo.sprites.ForEach(s =>
            {
                if (s.name == null)
                    return;
                var assetName = s.name;
                AutodetectAndAddDependency(fileInfo.DiskCache, assetName, fileInfo.Owner);
            });
        }

        TSFileAdditionalInfo AutodetectAndAddDependency(IDiskCache dc, string depName, IFileCache usedFrom)
        {
            var extension = PathUtils.GetExtension(depName);
            var depFile = dc.TryGetItem(depName) as IFileCache;
            if (depFile == null)
            {
                if (usedFrom != null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("In " + usedFrom.FullPath + " missing dependency " + depName);
                    Console.ForegroundColor = ConsoleColor.Gray;
                    TSFileAdditionalInfo.Get(usedFrom, dc).ReportDiag(true, -3, "Missing dependency " + depName, 0, 0, 0, 0);
                } else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Somethere missing dependency " + depName);
                    Console.ForegroundColor = ConsoleColor.Gray;
                }
                return null;
            }
            var assetFileInfo = TSFileAdditionalInfo.Get(depFile, dc);
            switch (extension)
            {
                case "css":
                    assetFileInfo.Type = FileCompilationType.Css;
                    CheckAdd(depName);
                    break;
                case "js":
                    assetFileInfo.Type = FileCompilationType.JavaScriptAsset;
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
