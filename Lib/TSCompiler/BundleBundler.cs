using System.Collections.Generic;
using Lib.Utils;
using Lib.ToolsDir;
using Lib.Bundler;
using System.Linq;
using System.IO;
using Lib.CSSProcessor;
using Lib.DiskCache;
using System.Globalization;

namespace Lib.TSCompiler
{
    public class BundleBundler : IBundlerCallback
    {
        string _mainJsBundleUrl;
        string _bundlePng;
        List<float> _bundlePngInfo;
        string _indexHtml;
        readonly IToolsDir _tools;

        public BundleBundler(IToolsDir tools)
        {
            _tools = tools;
        }

        public ProjectOptions Project;
        public BuildResult BuildResult;

        // value could be string or byte[] or Lazy<string|byte[]>
        public Dictionary<string, object> FilesContent;
        Dictionary<string, string> _jsFilesContent;

        public void Build(bool compress, bool mangle, bool beautify)
        {
            var diskCache = Project.Owner.DiskCache;
            var root = Project.Owner.Owner.FullPath;
            _jsFilesContent = new Dictionary<string, string>();
            var cssLink = "";
            var cssToBundle = new List<SourceFromPair>();
            foreach (var source in BuildResult.Path2FileInfo)
            {
                if (source.Value.Type == FileCompilationType.TypeScript ||
                    source.Value.Type == FileCompilationType.JavaScript ||
                    source.Value.Type == FileCompilationType.JavaScriptAsset)
                {
                    if (source.Value.Output == null)
                        continue; // Skip d.ts
                    _jsFilesContent[PathUtils.ChangeExtension(source.Key, "js").ToLowerInvariant()] =
                        source.Value.Output;
                }
                else if (source.Value.Type == FileCompilationType.Json)
                {
                    _jsFilesContent[source.Key.ToLowerInvariant() + ".js"] =
                        "Object.assign(module.exports, " + source.Value.Owner.Utf8Content + ");";
                }
                else if (source.Value.Type == FileCompilationType.Css)
                {
                    cssToBundle.Add(new SourceFromPair(source.Value.Owner.Utf8Content, source.Value.Owner.FullPath));
                }
                else if (source.Value.Type == FileCompilationType.Resource)
                {
                    FilesContent[source.Value.OutputUrl] = source.Value.Owner.ByteContent;
                }
            }

            if (cssToBundle.Count > 0)
            {
                string cssPath = Project.AllocateName("bundle.css");
                var cssProcessor = new CssProcessor(Project.Tools);
                FilesContent[cssPath] = cssProcessor.ConcatenateAndMinifyCss(cssToBundle, (string url, string from) =>
                {
                    var full = PathUtils.Join(from, url);
                    var fullJustName = full.Split('?', '#')[0];
                    var fileAdditionalInfo = BuildModuleCtx.AutodetectAndAddDependencyCore(Project, fullJustName,
                        diskCache.TryGetItem(from) as IFileCache);
                    FilesContent[fileAdditionalInfo.OutputUrl] = fileAdditionalInfo.Owner.ByteContent;
                    return PathUtils.SplitDirAndFile(fileAdditionalInfo.OutputUrl).Item2 +
                           full.Substring(fullJustName.Length);
                }).Result;
                cssLink += "<link rel=\"stylesheet\" href=\"" + cssPath + "\">";
            }

            if (Project.SpriteGeneration)
            {
                _bundlePng = Project.BundlePngUrl;
                var bundlePngContent = Project.SpriteGenerator.BuildImage(true);
                if (bundlePngContent != null)
                {
                    _bundlePngInfo = new List<float>();
                    foreach (var slice in bundlePngContent)
                    {
                        FilesContent[PathUtils.InjectQuality(_bundlePng, slice.Quality)] = slice.Content;
                        _bundlePngInfo.Add(slice.Quality);
                    }
                }
                else
                {
                    _bundlePng = null;
                }
            }

            var bundler = new BundlerImpl(_tools);
            bundler.Callbacks = this;
            if (Project.ExampleSources.Count > 0)
            {
                bundler.MainFiles = new[] {PathUtils.ChangeExtension(Project.ExampleSources[0], "js")};
            }
            else
            {
                bundler.MainFiles = new[] {PathUtils.ChangeExtension(Project.MainFile, "js")};
            }

            _mainJsBundleUrl = Project.BundleJsUrl;
            bundler.Compress = compress;
            bundler.Mangle = mangle;
            bundler.Beautify = beautify;
            var defines = new Dictionary<string, object>();
            foreach (var p in Project.Defines)
            {
                defines.Add(p.Key, p.Value);
            }

            bundler.Defines = defines;
            bundler.Bundle();
            if (!Project.NoHtml)
            {
                BuildFastBundlerIndexHtml(cssLink);
                FilesContent["index.html"] = _indexHtml;
            }
        }

        void BuildFastBundlerIndexHtml(string cssLink)
        {
            _indexHtml =
                $@"<!DOCTYPE html><html><head><meta charset=""utf-8"">{Project.HtmlHeadExpanded}<title>{Project.Title}</title>{cssLink}</head><body>{InitG11n()}<script src=""{_mainJsBundleUrl}"" charset=""utf-8""></script></body></html>";
        }

        string InitG11n()
        {
            if (!Project.Localize && _bundlePng == null)
                return "";
            var res = "<script>";
            if (Project.Localize)
            {
                Project.TranslationDb.BuildTranslationJs(_tools, FilesContent, Project.OutputSubDir);
                res +=
                    $"function g11nPath(s){{return\"./{(Project.OutputSubDir != null ? (Project.OutputSubDir + "/") : "")}\"+s.toLowerCase()+\".js\"}};";
                if (Project.DefaultLanguage != null)
                {
                    res += $"var g11nLoc=\"{Project.DefaultLanguage}\";";
                }
            }

            if (_bundlePng != null)
            {
                res += $"var bobrilBPath=\"{_bundlePng}\"";
                if (_bundlePngInfo.Count > 1)
                {
                    res += $",bobrilBPath2=[";
                    for (int i = 1; i < _bundlePngInfo.Count; i++)
                    {
                        var q = _bundlePngInfo[i];
                        if (i > 1) res += ",";
                        res +=
                            $"[\"{PathUtils.InjectQuality(_bundlePng, q)}\",{q.ToString(CultureInfo.InvariantCulture)}]";
                    }

                    res += "]";
                }
            }

            res += "</script>";
            return res;
        }

        public string ReadContent(string name)
        {
            if (_jsFilesContent.TryGetValue(name.ToLowerInvariant(), out var content))
            {
                return content;
            }

            throw new System.InvalidOperationException("Bundler Read Content does not exists:" + name);
        }

        public void WriteBundle(string name, string content)
        {
            FilesContent[name] = content;
        }

        public string GenerateBundleName(string forName)
        {
            if (forName == "")
                return _mainJsBundleUrl;
            return Project.AllocateName(forName.Replace("/", "_") + ".js");
        }

        public string ResolveRequire(string name, string from)
        {
            if (name.StartsWith("./") || name.StartsWith("../"))
            {
                return PathUtils.Join(PathUtils.Parent(from), name) + ".js";
            }

            var diskCache = Project.Owner.DiskCache;
            var moduleInfo = TSProject.FindInfoForModule(Project.Owner.Owner, diskCache, Project.Owner.Logger, name,
                out var diskName);
            if (moduleInfo == null)
                return null;
            var mainFile =
                PathUtils.ChangeExtension(PathUtils.Join(moduleInfo.Owner.FullPath, moduleInfo.MainFile), "js");
            return mainFile;
        }

        public string TslibSource(bool withImport)
        {
            return _tools.TsLibSource + (withImport ? _tools.ImportSource : "");
        }

        public IList<string> GetPlainJsDependencies(string name)
        {
            var diskCache = Project.Owner.DiskCache;
            var file = diskCache.TryGetItem(PathUtils.ChangeExtension(name, "ts")) as IFileCache;
            if (file == null)
            {
                file = diskCache.TryGetItem(PathUtils.ChangeExtension(name, "tsx")) as IFileCache;
            }

            if (file == null)
                return new List<string>();
            var fileInfo = TSFileAdditionalInfo.Get(file, diskCache);
            var sourceInfo = fileInfo.SourceInfo;
            if (sourceInfo == null || sourceInfo.assets == null)
                return new List<string>();
            return sourceInfo.assets.Select(i => i.name).Where(i => i.EndsWith(".js")).ToList();
        }
    }
}
