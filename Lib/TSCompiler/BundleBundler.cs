using System.Collections.Generic;
using Lib.Utils;
using Lib.ToolsDir;
using Lib.Bundler;
using System.Linq;
using Lib.CSSProcessor;
using System.Globalization;
using BTDB.Collections;
using System;
using System.Text.RegularExpressions;
using Njsast.Ast;
using Njsast.Bundler;
using Njsast.SourceMap;
using BundlerImpl = Lib.Bundler.BundlerImpl;

namespace Lib.TSCompiler
{
    public class BundleBundler : IBundlerCallback, IBundler
    {
        string _mainJsBundleUrl;
        string _bundlePng;
        List<float> _bundlePngInfo;
        string _indexHtml;
        readonly IToolsDir _tools;

        public BundleBundler(IToolsDir tools, MainBuildResult mainBuildResult, ProjectOptions project, BuildResult buildResult)
        {
            _tools = tools;
            _mainBuildResult = mainBuildResult;
            _project = project;
            _buildResult = buildResult;
        }

        readonly ProjectOptions _project;
        readonly BuildResult _buildResult;
        readonly MainBuildResult _mainBuildResult;
        RefDictionary<string, BundleBundler>? _subBundlers;

        public void Build(bool compress, bool mangle, bool beautify, bool _, string? __)
        {
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
                    _mainBuildResult.FilesContent.GetOrAddValueRef(_buildResult.ToOutputUrl(source)) = source.Owner.ByteContent;
                }
            }

            if (cssToBundle.Count > 0)
            {
                string cssPath = _mainBuildResult.AllocateName("bundle.css");
                var cssProcessor = new CssProcessor(_project.Tools);
                var cssContent = cssProcessor.ConcatenateAndMinifyCss(cssToBundle, (string url, string from) =>
                {
                    var full = PathUtils.Join(from, url);
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
                        _mainBuildResult.FilesContent.GetOrAddValueRef(PathUtils.InjectQuality(_bundlePng, slice.Quality)) =
                            slice.Content;
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
            if ((_project.ExampleSources?.Count ?? 0) > 0)
            {
                bundler.MainFiles = new[] {_project.ExampleSources[0]};
            }
            else
            {
                bundler.MainFiles = new[] {_project.MainFile};
            }

            _mainJsBundleUrl = _buildResult.BundleJsUrl;
            bundler.Compress = compress;
            bundler.Mangle = mangle;
            bundler.Beautify = beautify;
            bundler.Defines = _project.BuildDefines(_mainBuildResult);
            bundler.Bundle();
            if (!_project.NoHtml)
            {
                BuildFastBundlerIndexHtml(cssLink);
                _mainBuildResult.FilesContent.GetOrAddValueRef("index.html") = _indexHtml;
            }

            if (_project.SubProjects != null)
            {
                var newSubBundlers = new RefDictionary<string, BundleBundler>();
                foreach (var (projPath, subProject) in _project.SubProjects.OrderBy(a=>a.Value!.Variant=="serviceworker"))
                {
                    if (_subBundlers == null || !_subBundlers.TryGetValue(projPath, out var subBundler))
                    {
                        subBundler = new BundleBundler(_tools, _mainBuildResult, subProject, _buildResult.SubBuildResults.GetOrFakeValueRef(projPath));
                    }

                    newSubBundlers.GetOrAddValueRef(projPath) = subBundler;
                    subBundler.Build(compress, mangle, beautify, false, null);
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
                _project.TranslationDb.BuildTranslationJs(_tools, _mainBuildResult.FilesContent, _mainBuildResult.OutputSubDir);
                res +=
                    $"function g11nPath(s){{return\"./{(_mainBuildResult.OutputSubDir != null ? (_mainBuildResult.OutputSubDir + "/") : "")}\"+s.toLowerCase()+\".js\"}};";
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
            }

            res += ";";
            return res;
        }

        public string ReadContent(string name)
        {
            if (!_buildResult.Path2FileInfo.TryGetValue(name, out var fileInfo))
            {
                throw new InvalidOperationException("Bundler ReadContent does not exists:" + name);
            }

            if (fileInfo.Type == FileCompilationType.ImportedCss || fileInfo.Type == FileCompilationType.Css)
                return "";
            if (fileInfo.Type == FileCompilationType.Json)
            {
                return fileInfo.Owner.Utf8Content;
            }

            if (fileInfo.Type == FileCompilationType.JavaScriptAsset ||
                fileInfo.Type == FileCompilationType.JavaScript || fileInfo.Type == FileCompilationType.EsmJavaScript)
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
                _project.ApplySourceInfo(sourceReplacer, fileInfo.SourceInfo, _buildResult);
                sourceReplacer.Apply(adder);
                return sourceMapBuilder.Content();
            }

            throw new InvalidOperationException("Bundler Read Content unknown type " +
                                                Enum.GetName(typeof(FileCompilationType), fileInfo.Type) + ":" + name);
        }

        public void WriteBundle(string name, string content)
        {
            if (name == _mainJsBundleUrl)
                content = InitG11n() + content;
            _mainBuildResult.FilesContent.GetOrAddValueRef(name) = content;
        }

        public string GenerateBundleName(string forName)
        {
            if (forName == "")
                return _mainJsBundleUrl;
            return _mainBuildResult.AllocateName(forName.Replace("/", "_") + ".js");
        }

        public string ResolveRequire(string name, string from)
        {
            if (!_buildResult.ResolveCache.TryGetValue((from, name), out var resolveResult))
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
            return BundlerHelpers.JsHeaders(withImport);
        }

        public IList<string> GetPlainJsDependencies(string name)
        {
            if (!_buildResult.Path2FileInfo.TryGetValue(name, out var fileInfo))
            {
                throw new InvalidOperationException("Bundler GetPlainJsDependencies does not exists:" + name);
            }

            var sourceInfo = fileInfo.SourceInfo;
            if (sourceInfo == null || sourceInfo.Assets == null)
                return new List<string>();
            return sourceInfo.Assets.Select(i => i.Name).Where(i => i != null && !i.StartsWith("resource:") && i.EndsWith(".js"))
                .ToList()!;
        }
    }
}
