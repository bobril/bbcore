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

namespace Lib.TSCompiler
{
    public class BuildModuleCtx : ITSCompilerCtx, IImportResolver
    {
        public BuildCtx _buildCtx;
        public TSProject _owner;
        public BuildResult _result;
        public int IterationId;

        public void AddSource(TSFileAdditionalInfo file)
        {
            _result.Path2FileInfo[file.Owner.FullPath] = file;
        }

        public string resolveLocalImport(string name, TSFileAdditionalInfo parentInfo)
        {
            return ResolveImport(parentInfo.Owner.FullPath, name);
        }

        static readonly string[] ExtensionsToImport = { ".tsx", ".ts", ".d.ts", ".jsx", ".js" };
        static readonly string[] ExtensionsToImportFromJs = { ".jsx", ".js", "" };

        internal OrderedHashSet<string> ToCheck;
        internal uint CrawledCount;
        internal HashSet<TSFileAdditionalInfo> ResultSet = new HashSet<TSFileAdditionalInfo>();

        static bool IsDts(ReadOnlySpan<char> name)
        {
            return name.EndsWith(".d.ts");
        }

        static bool IsTsOrTsx(ReadOnlySpan<char> name)
        {
            return name.EndsWith(".ts") || name.EndsWith(".tsx");
        }

        public TSProject ResolveModule(string name)
        {
            if (_result.Modules.TryGetValue(name, out var module))
            {
                if (module.IterationId == IterationId)
                    return module.Valid ? module : null;
                for (uint i = 0; i < module.NegativeChecks.Count; i++)
                {
                    if (CheckItemExistence(module.NegativeChecks[i])) goto again;
                }
                if (module.Valid)
                {
                    module.LoadProjectJson(true);
                    if (module.PackageJsonChangeId == -1) goto again;
                }
                module.IterationId = IterationId;
                return module.Valid ? module : null;
            }
        again:;
            var negativeChecks = new StructList<string>();
            var dir = _owner.Owner.FullPath;
            while (dir.Length > 0)
            {
                var dc = _owner.DiskCache.TryGetItem(dir + "/node_modules/" + name) as IDirectoryCache;
                if (dc == null || dc.IsInvalid)
                {
                    negativeChecks.Add(dir + "/node_modules/" + name);
                }
                else
                {
                    if (dc.Name != name)
                    {

                    }
                    module = TSProject.Create(dc, _owner.DiskCache, _owner.Logger, dc.Name);
                    module.LoadProjectJson(true);
                    if (module.PackageJsonChangeId != -1)
                    {
                        module.NegativeChecks.AddRange(negativeChecks.AsSpan());
                        module.IterationId = IterationId;
                        _result.Modules[name] = module;
                        return module;
                    }
                }
                dir = PathUtils.Parent(dir).ToString();
            }
            module = TSProject.CreateInvalid(name);
            module.NegativeChecks.TransferFrom(ref negativeChecks);
            module.IterationId = IterationId;
            _result.Modules[name] = module;
            return null;
        }

        public bool CheckFileExistence(string name)
        {
            var f = _owner.DiskCache.TryGetItem(name) as IFileCache;
            return f != null && !f.IsInvalid;
        }

        public bool CheckItemExistence(string name)
        {
            var f = _owner.DiskCache.TryGetItem(name);
            return f != null && !f.IsInvalid;
        }

        public void ReportMissingImport(string from, string name)
        {
            if (_result.Path2FileInfo.TryGetValue(from, out var parentInfo))
            {
                parentInfo.ReportDiag(false, -15, "Cannot resolve import '" + name + "'", 0, 0, 0, 0);
            }
        }

        // returns "?" if error in resolving
        public string ResolveImport(string from, string name)
        {
            if (_result.ResolveCache.TryGetValue((from, name), out var res))
            {
                if (res.IterationId == IterationId) return res.FileName;
                if (res.FileName != null && !CheckFileExistence(res.FileName)) goto again;
                for (uint i = 0; i < res.NegativeChecks.Count; i++)
                {
                    if (CheckItemExistence(res.NegativeChecks[i])) goto again;
                }
            }
        again:;
            if (res == null)
            {
                res = new ResolveResult();
                _result.ResolveCache.Add((from, name), res);
            }
            else
            {
                res.FileName = null;
                res.Module = null;
                res.NegativeChecks.Clear();
            }
            res.IterationId = IterationId;
            var relative = name.StartsWith("./") || name.StartsWith("../");
            _result.Path2FileInfo.TryGetValue(from, out var parentInfo);
            string fn = null;
            if (relative)
            {
                fn = PathUtils.Join(parentInfo.Owner.Parent.FullPath, name);
            }
        relative:;
            if (relative)
            {
                if (fn.EndsWith(".json") || fn.EndsWith(".css"))
                {
                    var fc = _owner.DiskCache.TryGetItem(fn) as IFileCache;
                    if (fc != null && !fc.IsInvalid)
                    {
                        res.FileName = fn;
                        CheckAdd(fn, fn.EndsWith(".json") ? FileCompilationType.Json : FileCompilationType.ImportedCss);
                    }
                    else
                    {
                        res.NegativeChecks.Add(fn);
                        res.FileName = "?";
                    }
                    return res.FileName;
                }

                var dirPath = PathUtils.Parent(fn).ToString();
                var fileOnly = fn.Substring(dirPath.Length + 1);
                IFileCache item = null;
                var dc = _owner.DiskCache.TryGetItem(dirPath) as IDirectoryCache;
                if (dc == null || dc.IsInvalid)
                {
                    res.FileName = "?";
                    res.NegativeChecks.Add(dirPath.ToString());
                    return res.FileName;
                }

                item = (parentInfo.Type == FileCompilationType.EsmJavaScript ? ExtensionsToImportFromJs : ExtensionsToImport).Select(ext =>
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
                        CheckAdd(jsItem.FullPath, FileCompilationType.JavaScript);
                        parentInfo.ReportDependency(jsItem.FullPath);
                    }
                    else
                    {
                        res.NegativeChecks.Add(dirPath + "/" + fileOnly + ".js");
                        // implementation for .d.ts file does not have same name, it needs to be added to build by b.asset("lib.js") and cannot have dependencies
                    }
                }
                else
                {
                    CheckAdd(item.FullPath, IsTsOrTsx(item.Name) ? FileCompilationType.TypeScript : FileCompilationType.EsmJavaScript);
                }
                return res.FileName;
            }
            else
            {
                if (!_owner.ProjectOptions.AllowModuleDeepImport)
                {
                    if (!IsDts(parentInfo.Owner.Name) && (name.Contains('/') || name.Contains('\\')))
                    {
                        parentInfo.ReportDiag(true, -10, "Absolute import '" + name + "' must be just simple module name",
                            0, 0, 0, 0);
                        return null;
                    }
                }
                var pos = 0;
                PathUtils.EnumParts(name, ref pos, out var mn, out _);
                var mname = mn.ToString();
                var moduleInfo = ResolveModule(mname);
                res.Module = moduleInfo;
                if (moduleInfo == null)
                {
                    ReportMissingImport(from, name);
                    res.FileName = "?";
                    return res.FileName;
                }
                if (mname != moduleInfo.Name)
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

                var mainFile = PathUtils.Join(moduleInfo.Owner.FullPath, moduleInfo.MainFile);
                res.FileName = mainFile;
                CheckAdd(mainFile, IsTsOrTsx(mainFile) ? FileCompilationType.TypeScript : moduleInfo.MainFileNeedsToBeCompiled ? FileCompilationType.EsmJavaScript : FileCompilationType.JavaScript);

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

        void TryToResolveFromBuildCache(TSFileAdditionalInfo itemInfo)
        {
            itemInfo.TakenFromBuildCache = false;
            var bc = _owner.ProjectOptions.BuildCache;
            if (bc.IsEnabled)
            {
                var hashOfContent = itemInfo.Owner.HashOfContent;
                var confId = _owner.ProjectOptions.ConfigurationBuildCacheId;
                var fbc = bc.FindTSFileBuildCache(hashOfContent, confId);
                if (fbc != null)
                {
                    // TODO
                    itemInfo.TakenFromBuildCache = true;
                }
            }
        }

        public string resolveModuleMain(string name, TSFileAdditionalInfo parentInfo)
        {
            return ResolveImport(parentInfo.Owner.FullPath, name);
        }

        public void reportDiag(bool isError, int code, string text, string fileName, int startLine, int startCharacter,
            int endLine, int endCharacter)
        {
            var fc = _owner.DiskCache.TryGetItem(fileName) as IFileCache;
            if (fc == null)
            {
                throw new Exception("Cannot found " + fileName);
            }

            var fi = TSFileAdditionalInfo.Create(fc, _owner.DiskCache);
            fi.ReportDiag(isError, code, text, startLine, startCharacter, endLine, endCharacter);
        }

        public TSFileAdditionalInfo CheckAdd(string fullNameWithExtension, FileCompilationType compilationType)
        {
            if (!_result.Path2FileInfo.TryGetValue(fullNameWithExtension, out var info))
            {
                var fc = _owner.DiskCache.TryGetItem(fullNameWithExtension) as IFileCache;
                if (fc == null || fc.IsInvalid) return null;
                info = TSFileAdditionalInfo.Create(fc, _owner.DiskCache);
                info.Type = compilationType;
                _result.CommonSourceDirectory = PathUtils.CommonDir(_result.CommonSourceDirectory, fc.FullPath);
                _result.Path2FileInfo.Add(fullNameWithExtension, info);
            }
            else
            {
                if (info.Owner.IsInvalid)
                {
                    _result.Path2FileInfo.Remove(fullNameWithExtension);
                    return null;
                }
            }
            if (!ToCheck.Contains(fullNameWithExtension))
                ToCheck.Add(fullNameWithExtension);
            return info;
        }


        public string ExpandHtmlHead(string htmlHead)
        {
            return new Regex("<<[^>]+>>").Replace(htmlHead,
                (Match m) =>
                {
                    var info = AutodetectAndAddDependency(
                        PathUtils.Join(_owner.Owner.FullPath, m.Value.Substring(2, m.Length - 4)));
                    if (info == null)
                    {
                        _owner.Logger.Error("HtmlHead in package.json missing dependency " + m.Value.Substring(2, m.Length - 4));
                        return "";
                    }
                    return _result.ToOutputUrl(info);
                });
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

        public TSFileAdditionalInfo CrawlFile(string fileName)
        {
            var fileCache = _owner.DiskCache.TryGetItem(fileName) as IFileCache;
            if (fileCache == null || fileCache.IsInvalid)
            {
                if (_buildCtx.Verbose)
                    Console.WriteLine("Crawl skipping missing file " + fileName);
                return null;
            }

            if (!_result.Path2FileInfo.TryGetValue(fileName, out var info))
            {
                info = TSFileAdditionalInfo.Create(fileCache, _owner.DiskCache);
                info.Type = FileCompilationType.Unknown;
                _result.Path2FileInfo.Add(fileName, info);
            }

            ResultSet.Add(info);

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
                    if (ext == "css") info.Type = FileCompilationType.Css;
                    else if (ext == "js" || ext == "jsx") info.Type = FileCompilationType.EsmJavaScript;
                    else info.Type = FileCompilationType.Resource;
                }
            }

            if (info.IterationId != IterationId)
            {
                info.IterationId = IterationId;
                if (info.Owner.ChangeId != info.ChangeId)
                {
                    info.ChangeId = info.Owner.ChangeId;
                    _result.RecompiledLast.Add(info);
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
                            info.Output = null;
                            info.MapLink = null;
                            info.SourceInfo = null;
                            Transpile(info);
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
                            if (!_owner.ProjectOptions.BundleCss)
                            {
                                var cssProcessor = _buildCtx.CompilerPool.GetCss();
                                try
                                {
                                    info.Output = cssProcessor.ProcessCss(info.Owner.Utf8Content,
        ((TSFileAdditionalInfo)info).Owner.FullPath, (string url, string from) =>
                                        {
                                            var full = PathUtils.Join(from, url);
                                            var fullJustName = full.Split('?', '#')[0];
                                            var fileAdditionalInfo =
                                                AutodetectAndAddDependency(fullJustName);
                                            if (fileAdditionalInfo == null)
                                            {
                                                info.ReportDiag(true, -3, "Missing dependency " + url, 0, 0, 0, 0);
                                            }
                                            info.ReportDependency(fullJustName);
                                            return PathUtils.Subtract(fileAdditionalInfo.OutputUrl,
                                                        PathUtils.Parent(info.OutputUrl).ToString()) +
                                                    full.Substring(fullJustName.Length);
                                        }).Result;
                                }
                                finally
                                {
                                    _buildCtx.CompilerPool.ReleaseCss(cssProcessor);
                                }
                            }
                            break;
                    }
                }
            }
            foreach (var dep in info.Dependencies)
            {
                CheckAdd(dep, FileCompilationType.Unknown);
            }
            return info;
        }

        void Transpile(TSFileAdditionalInfo info)
        {
            ITSCompiler compiler = null;
            try
            {
                compiler = _buildCtx.CompilerPool.GetTs();
                compiler.DiskCache = _owner.DiskCache;
                compiler.CompilerOptions = _owner.ProjectOptions.FinalCompilerOptions;
                var result = compiler.Transpile(info.Owner.FullPath, info.Owner.Utf8Content);
                info.Output = SourceMap.RemoveLinkToSourceMap(result.JavaScript);
                info.MapLink = SourceMap.Parse(result.SourceMap, info.Owner.Parent.FullPath);
                info.ReportDiag(result.Diagnostics);
            }
            finally
            {
                if (compiler != null)
                    _buildCtx.CompilerPool.ReleaseTs(compiler);
            }

            var parser = new Parser(new Options(), info.Output);
            var toplevel = parser.Parse();
            toplevel.FigureOutScope();
            var ctx = new ResolvingConstEvalCtx(info.Owner.FullPath, this);
            var sourceInfo = GatherBobrilSourceInfo.Gather(toplevel, ctx, (IConstEvalCtx myctx, string text) =>
            {
                var res = PathUtils.Join(PathUtils.Parent(myctx.SourceName), text);
                return res;
            });
            info.SourceInfo = sourceInfo;
            AddDependenciesFromSourceInfo(info);
        }

        public void AddDependenciesFromSourceInfo(TSFileAdditionalInfo fileInfo)
        {
            var sourceInfo = fileInfo.SourceInfo;
            if (sourceInfo == null)
                return;
            sourceInfo.Imports?.ForEach(i =>
            {
                fileInfo.ReportDependency(ResolveImport(fileInfo.Owner.FullPath, i.Name));
            });
            sourceInfo.Assets?.ForEach(a =>
            {
                if (a.Name == null)
                    return;
                var assetName = a.Name;
                if (assetName.StartsWith("resource:"))
                {
                    assetName = assetName.Substring(9);
                    if (ReportDependency(fileInfo, AutodetectAndAddDependency(assetName, true)) == null)
                    {
                        fileInfo.ReportDiag(true, -3, "Missing dependency " + assetName, a.StartLine, a.StartCol, a.EndLine, a.EndCol);
                    }
                }
                else
                {
                    if (ReportDependency(fileInfo, AutodetectAndAddDependency(assetName)) == null)
                    {
                        fileInfo.ReportDiag(true, -3, "Missing dependency " + assetName, a.StartLine, a.StartCol, a.EndLine, a.EndCol);
                    }
                }
            });
            if (sourceInfo.Sprites != null)
            {
                if (_owner.ProjectOptions.SpriteGeneration)
                {
                    var spriteHolder = _owner.ProjectOptions.SpriteGenerator;
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
                            fileInfo.ReportDiag(true, -3, "Missing dependency " + assetName, s.NameStartLine, s.NameStartCol, s.NameEndLine, s.NameEndCol);
                        }
                    });
                }
            }
            if (sourceInfo.Translations != null)
            {
                var trdb = _owner.ProjectOptions.TranslationDb;
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
                                    "Problem with translation message \"" + t.Message + "\" " + err.ToString(), t.StartLine, t.StartCol, t.EndLine, t.EndCol);
                            }
                        }

                        if (t.JustFormat)
                            return;
                        var id = trdb.AddToDB(t.Message, t.Hint, t.WithParams);
                        var finalId = trdb.MapId(id);
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
            return CheckAdd(depName, forceResource ? FileCompilationType.Resource : FileCompilationType.Unknown);
        }

        public string readFile(string fullPath)
        {
            var file = TryGetFile(fullPath);
            if (file == null)
            {
                return null;
            }
            TSFileAdditionalInfo.Create(file, _owner.DiskCache).StartCompiling();
            return file.Utf8Content;
        }

        public IFileCache TryGetFile(string fullPath)
        {
            var file = _owner.DiskCache.TryGetItem(fullPath) as IFileCache;
            return file;
        }

        public string ResolveName(JsModule module)
        {
            return ResolveImport(module.ImportedFrom, module.Name);
        }

        public string LoadContent(string fileName)
        {
            var info = CrawlFile(fileName);
            return info.Output;
        }
    }
}
