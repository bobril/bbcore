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
using Njsast.Ast;
using Njsast.Bundler;
using Njsast.Compress;
using Njsast.Output;
using Njsast.Reader;
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

        public NjsastBundleBundler(IToolsDir tools, ILogger logger, MainBuildResult mainBuildResult,
            ProjectOptions project, BuildResult buildResult)
        {
            _tools = tools;
            _logger = logger;
            _mainBuildResult = mainBuildResult;
            _project = project;
            _buildResult = buildResult;
        }

        readonly ProjectOptions _project;
        readonly BuildResult _buildResult;
        readonly MainBuildResult _mainBuildResult;
        bool BuildSourceMap;
        string? SourceMapSourceRoot;
        RefDictionary<string, NjsastBundleBundler>? _subBundlers;

        public void Build(bool compress, bool mangle, bool beautify, bool buildSourceMap, string? sourceMapSourceRoot)
        {
            BuildSourceMap = buildSourceMap;
            SourceMapSourceRoot = sourceMapSourceRoot;
            var cssLink = "";
            var cssToBundle = new List<SourceFromPair>();
            foreach (var source in _buildResult.Path2FileInfo.Values.OrderBy(f => f.Owner.FullPath).ToArray())
            {
                if (source.Type == FileCompilationType.Css || source.Type == FileCompilationType.ImportedCss)
                {
                    cssToBundle.Add(new SourceFromPair(source.Owner.Utf8Content, source.Owner.FullPath));
                }
                else if (source.Type == FileCompilationType.Resource)
                {
                    _mainBuildResult.FilesContent.GetOrAddValueRef(_buildResult.ToOutputUrl(source)) =
                        source.Owner.ByteContent;
                }
            }

            if (cssToBundle.Count > 0)
            {
                string cssPath = _mainBuildResult.AllocateName("bundle.css");
                var cssProcessor = new CssProcessor(_project.Tools);
                var cssContent = cssProcessor.ConcatenateAndMinifyCss(cssToBundle, (string url, string from) =>
                {
                    var full = PathUtils.Join(@from, url);
                    var fullJustName = full.Split('?', '#')[0];
                    _buildResult.Path2FileInfo.TryGetValue(fullJustName, out var fileAdditionalInfo);
                    _mainBuildResult.FilesContent.GetOrAddValueRef(_buildResult.ToOutputUrl(fileAdditionalInfo)) =
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

                _mainBuildResult.FilesContent.GetOrAddValueRef(cssPath) = cssImports + cssContent;
                cssLink += "<link rel=\"stylesheet\" href=\"" + cssPath + "\">";
            }

            if (_project.SpriteGeneration)
            {
                _bundlePng = _project.BundlePngUrl;
                var bundlePngContent = _project.SpriteGenerator.BuildImage(true);
                if (bundlePngContent != null)
                {
                    _bundlePngInfo = new List<float>();
                    foreach (var slice in bundlePngContent)
                    {
                        _mainBuildResult.FilesContent.GetOrAddValueRef(
                                PathUtils.InjectQuality(_bundlePng, slice.Quality)) =
                            slice.Content;
                        _bundlePngInfo.Add(slice.Quality);
                    }
                }
                else
                {
                    _bundlePng = null;
                }
            }

            _mainJsBundleUrl = _buildResult.BundleJsUrl;

            var bundler = new BundlerImpl(this);
            if ((_project.ExampleSources?.Count ?? 0) > 0)
            {
                bundler.PartToMainFilesMap = new Dictionary<string, IReadOnlyList<string>>
                    {{"Bundle", new[] {_project.ExampleSources[0]}}};
            }
            else
            {
                bundler.PartToMainFilesMap = new Dictionary<string, IReadOnlyList<string>>
                    {{"Bundle", new[] {_project.MainFile}}};
            }

            bundler.CompressOptions = compress ? CompressOptions.FastDefault : null;
            bundler.Mangle = mangle;
            bundler.OutputOptions = new OutputOptions {Beautify = beautify, ShortenBooleans = !beautify};
            bundler.GenerateSourceMap = BuildSourceMap;
            bundler.GlobalDefines = _project.BuildDefines(_mainBuildResult);
            bundler.Run();
            if (!_project.NoHtml)
            {
                BuildFastBundlerIndexHtml(cssLink);
                _mainBuildResult.FilesContent.GetOrAddValueRef("index.html") = _indexHtml;
            }

            if (_project.SubProjects != null)
            {
                var newSubBundlers = new RefDictionary<string, NjsastBundleBundler>();
                foreach (var (projPath, subProject) in _project.SubProjects.OrderBy(a =>
                    a.Value!.Variant == "serviceworker"))
                {
                    if (_subBundlers == null || !_subBundlers.TryGetValue(projPath, out var subBundler))
                    {
                        subBundler = new NjsastBundleBundler(_tools, _logger, _mainBuildResult, subProject,
                            _buildResult.SubBuildResults.GetOrFakeValueRef(projPath));
                    }

                    newSubBundlers.GetOrAddValueRef(projPath) = subBundler;
                    subBundler.Build(compress, mangle, beautify, buildSourceMap, sourceMapSourceRoot);
                }

                _subBundlers = newSubBundlers;
            }
            else
            {
                _subBundlers = null;
            }
        }

        void BuildFastBundlerIndexHtml(string cssLink)
        {
            _indexHtml =
                $@"<!DOCTYPE html><html><head><meta charset=""utf-8"">{_project.ExpandHtmlHead(_buildResult)}<title>{_project.Title}</title>{cssLink}</head><body><script src=""{_mainJsBundleUrl}"" charset=""utf-8""></script></body></html>";
        }

        string InitG11n()
        {
            if (!_project.Localize && _bundlePng == null)
                return "";
            var res = "";
            if (_project.Localize)
            {
                _project.TranslationDb.BuildTranslationJs(_tools, _mainBuildResult.FilesContent,
                    _mainBuildResult.OutputSubDir);
                res +=
                    $"function g11nPath(s){{return\"./{_mainBuildResult.OutputSubDirPrefix}\"+s.toLowerCase()+\".js\"}};";
                if (_project.DefaultLanguage != null)
                {
                    res += $"var g11nLoc=\"{_project.DefaultLanguage}\";";
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

                res += ";";
            }

            return res;
        }

        public (string?, SourceMap?) ReadContent(string name)
        {
            if (name == "<empty>")
            {
                return ("module.exports = {};", null);
            }
            if (!_buildResult.Path2FileInfo.TryGetValue(name, out var fileInfo))
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
                fileInfo.Type == FileCompilationType.JavaScript)
            {
                if (BuildSourceMap)
                    return (fileInfo.Output, SourceMap.Identity(fileInfo.Output, fileInfo.Owner.FullPath));
                return (fileInfo.Output, null);
            }

            if (fileInfo.Type == FileCompilationType.TypeScriptDefinition)
            {
                return ("", null);
            }

            if (fileInfo.Type == FileCompilationType.TypeScript || fileInfo.Type == FileCompilationType.EsmJavaScript)
            {
                if (BuildSourceMap)
                {
                    var sourceMapBuilder = new SourceMapBuilder();
                    var adder = sourceMapBuilder.CreateSourceAdder(fileInfo.Output, fileInfo.MapLink);
                    var sourceReplacer = new SourceReplacer();
                    _project.ApplySourceInfo(sourceReplacer, fileInfo.SourceInfo, _buildResult);
                    sourceReplacer.Apply(adder);
                    return (sourceMapBuilder.Content(), sourceMapBuilder.Build(".", "."));
                }
                else
                {
                    var sourceMapBuilder = new SourceMapBuilder();
                    var adder = sourceMapBuilder.CreateSourceAdder(fileInfo.Output, fileInfo.MapLink);
                    var sourceReplacer = new SourceReplacer();
                    _project.ApplySourceInfo(sourceReplacer, fileInfo.SourceInfo, _buildResult);
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
            _mainBuildResult.FilesContent.GetOrAddValueRef(name) = content;
        }

        public void WriteBundle(string name, SourceMapBuilder content)
        {
            content.AddText("//# sourceMappingURL=" + name + ".map");
            var source = content.Content();
            var sm = content.Build(_mainBuildResult.CommonSourceDirectory, SourceMapSourceRoot ?? "..").ToString();
            _logger.Info("Bundler created " + name + " with " + source.Length + " chars and sourcemap with " +
                         sm.Length + " chars");
            _mainBuildResult.FilesContent.GetOrAddValueRef(name) = source;
            _mainBuildResult.FilesContent.GetOrAddValueRef(name + ".map") = sm;
        }

        public void ReportTime(string name, TimeSpan duration)
        {
            _logger.Info("Bundler phase " + name + " took " +
                         duration.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture) + "s");
        }

        public void ModifyBundle(string name, AstToplevel topLevelAst)
        {
            if (name != _mainJsBundleUrl)
                return;
            var srcToInject = InitG11n();
            if (srcToInject == "")
                return;
            topLevelAst.Body.InsertRange(0, Parser.Parse(srcToInject).Body);
        }

        public string GenerateBundleName(string forName)
        {
            if (forName == "Bundle")
                return _mainJsBundleUrl;
            return _mainBuildResult.AllocateName(forName.Replace("/", "_") + ".js");
        }

        public string ResolveRequire(string name, string from)
        {
            if (!_buildResult.ResolveCache.TryGetValue((@from, name), out var resolveResult))
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
            if (name == "<empty>")
                return Array.Empty<string>();

            if (!_buildResult.Path2FileInfo.TryGetValue(name, out var fileInfo))
            {
                throw new InvalidOperationException("Bundler GetPlainJsDependencies does not exists:" + name);
            }

            var sourceInfo = fileInfo.SourceInfo;
            if (sourceInfo?.Assets == null)
                return Array.Empty<string>();
            return sourceInfo.Assets.Select(i => i.Name).Where(i => !i.StartsWith("resource:") && i.EndsWith(".js"))
                .ToList();
        }
    }
}
