using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using BTDB.Collections;
using Lib.CSSProcessor;
using Lib.ToolsDir;
using Lib.Utils;
using Lib.Utils.Logger;
using Njsast.Bundler;
using Njsast.Compress;
using Njsast.Output;
using Njsast.SourceMap;

namespace Lib.TSCompiler
{
    public class NjsastBundleBundler : IBundler, IBundlerCtx
    {
        string _mainJsBundleUrl;
        string _bundlePng;
        List<float> _bundlePngInfo;
        string _indexHtml;
        readonly IToolsDir _tools;
        readonly ILogger _logger;

        public NjsastBundleBundler(IToolsDir tools, ILogger logger)
        {
            _tools = tools;
            _logger = logger;
        }

        public ProjectOptions Project { get; set; }
        public BuildResult BuildResult { get; set; }
        public bool BuildSourceMap { get; set; }
        public string? SourceMapSourceRoot { get; set; }

        // value could be string or byte[] or Lazy<string|byte[]>
        public RefDictionary<string, object> FilesContent { get; set; }

        public void Build(bool compress, bool mangle, bool beautify)
        {
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
                    var full = PathUtils.Join(@from, url);
                    var fullJustName = full.Split('?', '#')[0];
                    BuildResult.Path2FileInfo.TryGetValue(fullJustName, out var fileAdditionalInfo);
                    FilesContent.GetOrAddValueRef(BuildResult.ToOutputUrl(fileAdditionalInfo)) =
                        fileAdditionalInfo.Owner.ByteContent;
                    return PathUtils.GetFile(fileAdditionalInfo.OutputUrl) +
                           full.Substring(fullJustName.Length);
                }).Result;
                var cssImports = "";
                foreach (var match in Regex.Matches(cssContent, "@import .*;"))
                {
                    cssImports += match.ToString();
                    cssContent = cssContent.Replace(match.ToString(), "");
                }

                FilesContent.GetOrAddValueRef(cssPath) = cssImports + cssContent;
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
                        FilesContent.GetOrAddValueRef(PathUtils.InjectQuality(_bundlePng, slice.Quality)) =
                            slice.Content;
                        _bundlePngInfo.Add(slice.Quality);
                    }
                }
                else
                {
                    _bundlePng = null;
                }
            }

            _mainJsBundleUrl = BuildResult.BundleJsUrl;

            var bundler = new BundlerImpl(this);
            if (Project.ExampleSources.Count > 0)
            {
                bundler.PartToMainFilesMap = new Dictionary<string, IReadOnlyList<string>>
                    {{"Bundle", new[] {Project.ExampleSources[0]}}};
            }
            else
            {
                bundler.PartToMainFilesMap = new Dictionary<string, IReadOnlyList<string>>
                    {{"Bundle", new[] {Project.MainFile}}};
            }

            bundler.CompressOptions = compress ? CompressOptions.FastDefault : null;
            bundler.Mangle = mangle;
            bundler.OutputOptions = new OutputOptions {Beautify = beautify, ShortenBooleans = !beautify};
            bundler.GenerateSourceMap = BuildSourceMap;
            var defines = new Dictionary<string, object>();
            foreach (var p in Project.ExpandedDefines)
            {
                defines.Add(p.Key, p.Value.ConstValue());
            }

            bundler.GlobalDefines = defines;
            bundler.Run();
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

        public (string?, SourceMap?) ReadContent(string name)
        {
            if (!BuildResult.Path2FileInfo.TryGetValue(name, out var fileInfo))
            {
                throw new InvalidOperationException("Bundler ReadContent does not exists:" + name);
            }

            if (fileInfo.Type == FileCompilationType.ImportedCss || fileInfo.Type == FileCompilationType.Css)
                return ("", null);
            if (fileInfo.Type == FileCompilationType.Json)
            {
                if (BuildSourceMap)
                    return (fileInfo.Owner.Utf8Content,
                        SourceMap.Identity(fileInfo.Owner.Utf8Content, fileInfo.Owner.FullPath));
                return (fileInfo.Owner.Utf8Content, null);
            }

            if (fileInfo.Type == FileCompilationType.JavaScriptAsset ||
                fileInfo.Type == FileCompilationType.JavaScript || fileInfo.Type == FileCompilationType.EsmJavaScript)
            {
                if (BuildSourceMap)
                    return (fileInfo.Output, SourceMap.Identity(fileInfo.Output, fileInfo.Owner.FullPath));
                return (fileInfo.Output, null);
            }

            if (fileInfo.Type == FileCompilationType.TypeScriptDefinition)
            {
                return ("", null);
            }

            if (fileInfo.Type == FileCompilationType.TypeScript)
            {
                if (BuildSourceMap)
                {
                    var sourceMapBuilder = new SourceMapBuilder();
                    var adder = sourceMapBuilder.CreateSourceAdder(fileInfo.Output, fileInfo.MapLink);
                    var sourceReplacer = new SourceReplacer();
                    Project.ApplySourceInfo(sourceReplacer, fileInfo.SourceInfo, BuildResult);
                    sourceReplacer.Apply(adder);
                    return (sourceMapBuilder.Content(), sourceMapBuilder.Build(".", "."));
                }
                else
                {
                    var sourceMapBuilder = new SourceMapBuilder();
                    var adder = sourceMapBuilder.CreateSourceAdder(fileInfo.Output, fileInfo.MapLink);
                    var sourceReplacer = new SourceReplacer();
                    Project.ApplySourceInfo(sourceReplacer, fileInfo.SourceInfo, BuildResult);
                    sourceReplacer.Apply(adder);
                    return (sourceMapBuilder.Content(), null);
                }
            }

            throw new InvalidOperationException("Bundler Read Content unknown type " +
                                                Enum.GetName(typeof(FileCompilationType), fileInfo.Type) + ":" + name);
        }

        public string JsHeaders(string forSplit, bool withImport)
        {
            return BundlerHelpers.JsHeaders(withImport);
        }

        public void WriteBundle(string name, string content)
        {
            _logger.Info("Bundler created " + name + " with " + content.Length + " chars");
            FilesContent.GetOrAddValueRef(name) = content;
        }

        public void WriteBundle(string name, SourceMapBuilder content)
        {
            content.AddText("//# sourceMappingURL=" + name + ".map");
            var source = content.Content();
            var sm = content.Build(Project.CommonSourceDirectory, SourceMapSourceRoot ?? "..").ToString();
            _logger.Info("Bundler created " + name + " with " + source.Length + " chars and sourcemap with " +
                         sm.Length + " chars");
            FilesContent.GetOrAddValueRef(name) = source;
            FilesContent.GetOrAddValueRef(name + ".map") = sm;
        }

        public void ReportTime(string name, TimeSpan duration)
        {
            _logger.Info("Bundler phase " + name + " took " +
                         duration.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture) + "s");
        }

        public string GenerateBundleName(string forName)
        {
            if (forName == "Bundle")
                return _mainJsBundleUrl;
            return BuildResult.AllocateName(forName.Replace("/", "_") + ".js");
        }

        public string ResolveRequire(string name, string from)
        {
            if (!BuildResult.ResolveCache.TryGetValue((@from, name), out var resolveResult))
            {
                throw new Exception($"Bundler cannot resolve {name} from {@from}");
            }

            var res = resolveResult.FileNameWithPreference(false);
            if (res == "?")
            {
                throw new Exception($"Bundler failed to resolve {name} from {@from}");
            }

            return res;
        }

        public IEnumerable<string> GetPlainJsDependencies(string name)
        {
            if (!BuildResult.Path2FileInfo.TryGetValue(name, out var fileInfo))
            {
                throw new InvalidOperationException("Bundler GetPlainJsDependencies does not exists:" + name);
            }

            var sourceInfo = fileInfo.SourceInfo;
            if (sourceInfo?.Assets == null)
                return new List<string>();
            return sourceInfo.Assets.Select(i => i.Name).Where(i => !i.StartsWith("resource:") && i.EndsWith(".js"))
                .ToList();
        }
    }
}
