using System.Collections.Generic;
using Lib.Utils;
using Lib.ToolsDir;
using Lib.Bundler;
using System.Linq;
using Lib.CSSProcessor;
using System.Globalization;
using BTDB.Collections;
using System;
using Njsast.SourceMap;

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
        public RefDictionary<string, object> FilesContent;

        public void Build(bool compress, bool mangle, bool beautify)
        {
            var diskCache = Project.Owner.DiskCache;
            var cssLink = "";
            var cssToBundle = new List<SourceFromPair>();
            foreach (var source in BuildResult.Path2FileInfo.Values.OrderBy(f => f.Owner.FullPath).ToArray())
            {
                if (source.Type == FileCompilationType.Css || source.Type == FileCompilationType.ImportedCss)
                {
                    cssToBundle.Add(new SourceFromPair(source.Owner.Utf8Content, source.Owner.FullPath));
                }
                else if (source.Type == FileCompilationType.Resource)
                {
                    FilesContent.GetOrAddValueRef(BuildResult.ToOutputUrl(source)) = source.Owner.ByteContent;
                }
            }

            if (cssToBundle.Count > 0)
            {
                string cssPath = BuildResult.AllocateName("bundle.css");
                var cssProcessor = new CssProcessor(Project.Tools);
                var cssContent = cssProcessor.ConcatenateAndMinifyCss(cssToBundle, (string url, string from) =>
                {
                    var full = PathUtils.Join(from, url);
                    var fullJustName = full.Split('?', '#')[0];
                    BuildResult.Path2FileInfo.TryGetValue(fullJustName, out var fileAdditionalInfo);
                    FilesContent.GetOrAddValueRef(BuildResult.ToOutputUrl(fileAdditionalInfo)) = fileAdditionalInfo.Owner.ByteContent;
                    return PathUtils.GetFile(fileAdditionalInfo.OutputUrl) +
                           full.Substring(fullJustName.Length);
                }).Result;
                FilesContent.GetOrAddValueRef(cssPath) = cssContent;
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
                        FilesContent.GetOrAddValueRef(PathUtils.InjectQuality(_bundlePng, slice.Quality)) = slice.Content;
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
                bundler.MainFiles = new[] { Project.ExampleSources[0] };
            }
            else
            {
                bundler.MainFiles = new[] { Project.MainFile };
            }

            _mainJsBundleUrl = BuildResult.BundleJsUrl;
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
                FilesContent.GetOrAddValueRef("index.html") = _indexHtml;
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
            if (!BuildResult.Path2FileInfo.TryGetValue(name, out var fileInfo))
            {
                throw new InvalidOperationException("Bundler ReadContent does not exists:" + name);
            }
            if (fileInfo.Type == FileCompilationType.ImportedCss || fileInfo.Type == FileCompilationType.Css)
                return "";
            if (fileInfo.Type == FileCompilationType.Json)
            {
                return fileInfo.Owner.Utf8Content;
            }
            if (fileInfo.Type == FileCompilationType.JavaScriptAsset || fileInfo.Type == FileCompilationType.JavaScript || fileInfo.Type == FileCompilationType.EsmJavaScript)
            {
                return fileInfo.Output;
            }
            if (fileInfo.Type == FileCompilationType.TypeScriptDefinition)
            {
                return "";
            }
            if (fileInfo.Type == FileCompilationType.TypeScript)
            {
                var sourceMapBuilder = new SourceMapBuilder();
                var adder = sourceMapBuilder.CreateSourceAdder(fileInfo.Output, fileInfo.MapLink);
                var sourceReplacer = new SourceReplacer();
                Project.ApplySourceInfo(sourceReplacer, fileInfo.SourceInfo, BuildResult);
                sourceReplacer.Apply(adder);
                return sourceMapBuilder.Content();
            }
            throw new InvalidOperationException("Bundler Read Content unknown type " + Enum.GetName(typeof(FileCompilationType), fileInfo.Type) + ":" + name);
        }

        public void WriteBundle(string name, string content)
        {
            FilesContent.GetOrAddValueRef(name) = content;
        }

        public string GenerateBundleName(string forName)
        {
            if (forName == "")
                return _mainJsBundleUrl;
            return BuildResult.AllocateName(forName.Replace("/", "_") + ".js");
        }

        public string ResolveRequire(string name, string from)
        {
            if (!BuildResult.ResolveCache.TryGetValue((from, name), out var resolveResult))
            {
                throw new Exception($"Bundler cannot resolve {name} from {from}");
            }
            var res = resolveResult.FileNameWithPreference(false);
            if (res == "?")
            {
                throw new Exception($"Bundler failed to resolve {name} from {from}");
            }
            return res;
        }

        public string TslibSource(bool withImport)
        {
            return _tools.TsLibSource + (withImport ? _tools.ImportSource : "");
        }

        public IList<string> GetPlainJsDependencies(string name)
        {
            if (!BuildResult.Path2FileInfo.TryGetValue(name, out var fileInfo))
            {
                throw new InvalidOperationException("Bundler GetPlainJsDependencies does not exists:" + name);
            }
            var sourceInfo = fileInfo.SourceInfo;
            if (sourceInfo == null || sourceInfo.Assets == null)
                return new List<string>();
            return sourceInfo.Assets.Select(i => i.Name).Where(i => !i.StartsWith("resource:") && i.EndsWith(".js")).ToList();
        }
    }
}
