using System.Collections.Generic;
using System.Text;
using Lib.Utils;
using Lib.ToolsDir;
using System.Linq;
using Njsast.SourceMap;
using System;
using BTDB.Collections;
using Lib.DiskCache;
using Njsast.Bundler;
using Njsast.Coverage;
using Njsast.Output;
using Njsast.Reader;
using Njsast.Runtime;

namespace Lib.TSCompiler
{
    public class FastBundleBundler
    {
        string _bundleJs;
        SourceMap _sourceMap;
        string _sourceMapString;
        string _bundle2Js;
        SourceMap _sourceMap2;
        string _sourceMap2String;
        string _cssLink;
        string _indexHtml;
        string _versionDirPrefix;

        readonly IToolsDir _tools;

        public FastBundleBundler(IToolsDir tools, MainBuildResult mainBuildResult, ProjectOptions project,
            BuildResult buildResult)
        {
            _tools = tools;
            _mainBuildResult = mainBuildResult;
            _project = project;
            _buildResult = buildResult;
        }

        readonly ProjectOptions _project;
        readonly BuildResult _buildResult;
        readonly MainBuildResult _mainBuildResult;

        string _bundlePng;
        List<float> _bundlePngInfo;
        RefDictionary<string, FastBundleBundler>? _subBundlers;

        public Dictionary<string, SourceMap> SourceMaps;

        public void Build(string sourceRoot, bool testProj = false, bool allowIncremental = true)
        {
            _versionDirPrefix = "";
            var coverage = _project.CoverageEnabled;
            if (coverage) allowIncremental = false;
            if (_mainBuildResult.OutputSubDir != null)
                _versionDirPrefix = _mainBuildResult.OutputSubDir + "/";
            var root = _mainBuildResult.CommonSourceDirectory;
            var incremental = _buildResult.Incremental && allowIncremental;
            var start = DateTime.UtcNow;
            var sourceMapBuilder = new SourceMapBuilder();
            if (_project.Localize)
            {
                if (!incremental)
                {
                    sourceMapBuilder.AddText(
                        $"function g11nPath(s){{return\"./{_mainBuildResult.OutputSubDirPrefix}\"+s.toLowerCase()+\".js\"}};");
                    if (_project.DefaultLanguage != null)
                    {
                        sourceMapBuilder.AddText($"var g11nLoc=\"{_project.DefaultLanguage}\";");
                    }
                }
            }

            if (_project.SpriteGeneration)
            {
                _bundlePng = _project.BundlePngUrl;
                var bundlePngContent = _project.SpriteGenerator.BuildImage(false);
                if (bundlePngContent != null)
                {
                    _bundlePngInfo = new List<float>();
                    foreach (var slice in bundlePngContent)
                    {
                        _mainBuildResult.FilesContent.GetOrAddValueRef(
                            PathUtils.InjectQuality(_bundlePng, slice.Quality)) = slice.Content;
                        _bundlePngInfo.Add(slice.Quality);
                    }
                }
                else
                {
                    _bundlePng = null;
                }
            }

            if (_bundlePng != null && !incremental)
            {
                sourceMapBuilder.AddText(_mainBuildResult.GenerateCodeForBobrilBPath(_bundlePng, _bundlePngInfo));
            }

            if (!incremental)
            {
                sourceMapBuilder.AddText(_tools.LoaderJs);
                sourceMapBuilder.AddText(GetGlobalDefines());
                sourceMapBuilder.AddText(GetModuleMap());
                sourceMapBuilder.AddText(BundlerHelpers.JsHeaders(false));
            }

            var cssLink = "";

            var sortedResultSet = incremental
                ? _buildResult.RecompiledIncrementaly.OrderBy(f => f.Owner.FullPath).ToArray()
                : _buildResult.Path2FileInfo.Values.OrderBy(f => f.Owner.FullPath).ToArray();

            if (!incremental)
            {
                foreach (var source in _buildResult.JavaScriptAssets)
                {
                    sourceMapBuilder.AddSource(source.Output, source.MapLink);
                }
            }

            foreach (var source in sortedResultSet)
            {
                if (source.Type == FileCompilationType.TypeScript ||
                    source.Type == FileCompilationType.EsmJavaScript ||
                    source.Type == FileCompilationType.JavaScript)
                {
                    if (source.Output == null)
                        continue; // Skip d.ts
                    sourceMapBuilder.AddText(
                        $"R('{PathUtils.Subtract(PathUtils.WithoutExtension(source.Owner.FullPath), root)}',function(require, module, exports, global){{");
                    var adder = sourceMapBuilder.CreateSourceAdder(source.Output, source.MapLink);
                    var sourceReplacer = new SourceReplacer();
                    _project.ApplySourceInfo(sourceReplacer, source.SourceInfo, _buildResult);
                    sourceReplacer.Apply(adder);
                    sourceMapBuilder.AddText("\n});");
                }
                else if (source.Type == FileCompilationType.Json)
                {
                    sourceMapBuilder.AddText(
                        $"R('{PathUtils.Subtract(source.Owner.FullPath, root)}',");
                    sourceMapBuilder.AddText(source.Owner.Utf8Content);
                    sourceMapBuilder.AddText(");");
                }
                else if (source.Type == FileCompilationType.ImportedCss)
                {
                    sourceMapBuilder.AddText(
                        $"R('{PathUtils.Subtract(source.Owner.FullPath, root)}',function(){{}});");
                    string cssPath = _buildResult.ToOutputUrl(source);
                    _mainBuildResult.FilesContent.GetOrAddValueRef(cssPath) = source.Output;
                    cssLink += "<link rel=\"stylesheet\" href=\"" + cssPath + "\">";
                }
                else if (source.Type == FileCompilationType.Css)
                {
                    string cssPath = _buildResult.ToOutputUrl(source);
                    _mainBuildResult.FilesContent.GetOrAddValueRef(cssPath) = source.Output;
                    cssLink += "<link rel=\"stylesheet\" href=\"" + cssPath + "\">";
                }
                else if (source.Type == FileCompilationType.Resource)
                {
                    _mainBuildResult.FilesContent.GetOrAddValueRef(_buildResult.ToOutputUrl(source)) =
                        source.Owner.ByteContent;
                }
            }

            if (!testProj && _project.NoHtml)
            {
                sourceMapBuilder.AddText(RequireBobril());
                if (_project.MainFile != null)
                {
                    sourceMapBuilder.AddText(
                        $"R.r('./{PathUtils.WithoutExtension(PathUtils.Subtract(_project.MainFile, root))}');");
                }
            }

            if (_project.Localize)
            {
                _project.TranslationDb.BuildTranslationJs(_tools, _mainBuildResult.FilesContent,
                    _mainBuildResult.OutputSubDir);
            }

            if (incremental)
            {
                sourceMapBuilder.AddText("//# sourceMappingURL=bundle2.js.map");
                _sourceMap2 = sourceMapBuilder.Build(root, sourceRoot);
                _sourceMap2String = _sourceMap2.ToString();
                _bundle2Js = sourceMapBuilder.Content();
                _project.Owner.Logger.Info("JS Bundle length: " + _bundleJs.Length + " SourceMap length: " +
                                           _sourceMapString.Length + " Delta: " + _bundle2Js.Length + " SM:" +
                                           _sourceMap2String.Length + " T:" +
                                           (DateTime.UtcNow - start).TotalMilliseconds.ToString("F0") + "ms");
            }
            else
            {
                sourceMapBuilder.AddText("//# sourceMappingURL=" + PathUtils.GetFile(_buildResult.BundleJsUrl) +
                                         ".map");
                if (coverage)
                {
                    _sourceMap = sourceMapBuilder.Build(".", ".");
                    _bundleJs = sourceMapBuilder.Content();
                    var toplevel = Parser.Parse(_bundleJs);
                    _sourceMap.ResolveInAst(toplevel);
                    var coverageInst = new CoverageInstrumentation();
                    coverageInst.RealPath = PathUtils.RealPath;
                    _project.CoverageInstrumentation = coverageInst;
                    toplevel = coverageInst.Instrument(toplevel);
                    coverageInst.AddCountingHelpers(toplevel);
                    coverageInst.CleanUp(new SourceReader(_project.Owner.DiskCache,
                        _mainBuildResult.CommonSourceDirectory));
                    if (_project.MainFile != null)
                        MarkImportant(_project.MainFile, _buildResult, new HashSet<string>(), coverageInst);
                    sourceMapBuilder = new SourceMapBuilder();
                    toplevel.PrintToBuilder(sourceMapBuilder, new OutputOptions {Beautify = true});
                    sourceMapBuilder.AddText("//# sourceMappingURL=" + PathUtils.GetFile(_buildResult.BundleJsUrl) +
                                             ".map");
                }

                _sourceMap = sourceMapBuilder.Build(root, sourceRoot);
                _bundleJs = sourceMapBuilder.Content();

                _sourceMapString = _sourceMap.ToString();
                _sourceMap2 = null;
                _sourceMap2String = null;
                _bundle2Js = null;
                _cssLink = cssLink;
                _project.Owner.Logger.Info("JS Bundle length: " + _bundleJs.Length + " SourceMap length: " +
                                           _sourceMapString.Length + " T:" +
                                           (DateTime.UtcNow - start).TotalMilliseconds.ToString("F0") + "ms");
            }

            _mainBuildResult.FilesContent.GetOrAddValueRef(_buildResult.BundleJsUrl) = _bundleJs;
            _mainBuildResult.FilesContent.GetOrAddValueRef(_buildResult.BundleJsUrl + ".map") = _sourceMapString;
            if (incremental)
            {
                _mainBuildResult.FilesContent.GetOrAddValueRef(_versionDirPrefix + "bundle2.js") = _bundle2Js;
                _mainBuildResult.FilesContent.GetOrAddValueRef(_versionDirPrefix + "bundle2.js.map") =
                    _sourceMap2String;
                SourceMaps = new Dictionary<string, SourceMap>
                {
                    {PathUtils.GetFile(_buildResult.BundleJsUrl), _sourceMap},
                    {"bundle2.js", _sourceMap2}
                };
            }
            else
            {
                SourceMaps = new Dictionary<string, SourceMap>
                {
                    {PathUtils.GetFile(_buildResult.BundleJsUrl), _sourceMap}
                };
            }

            if (_project.SubProjects != null)
            {
                var newSubBundlers = new RefDictionary<string, FastBundleBundler>();
                foreach (var (projPath, subProject) in _project.SubProjects.OrderBy(a =>
                    a.Value?.Variant == "serviceworker"))
                {
                    if (subProject == null) continue;
                    if (_subBundlers == null || !_subBundlers.TryGetValue(projPath, out var subBundler))
                    {
                        subBundler = new FastBundleBundler(_tools, _mainBuildResult, subProject,
                            _buildResult.SubBuildResults.GetOrFakeValueRef(projPath));
                    }

                    newSubBundlers.GetOrAddValueRef(projPath) = subBundler;
                    subBundler.Build(sourceRoot, false, false);
                }

                _subBundlers = newSubBundlers;
            }
            else
            {
                _subBundlers = null;
            }
        }

        void MarkImportant(string fn, BuildResult buildResult, HashSet<string> visited,
            CoverageInstrumentation coverageInst)
        {
            if (visited.Contains(fn))
                return;
            visited.Add(fn);
            if (!buildResult.Path2FileInfo.TryGetValue(fn, out var fi))
                return;
            foreach (var dependency in fi.Dependencies)
            {
                MarkImportant(dependency, buildResult, visited, coverageInst);
            }

            if (coverageInst.InstrumentedFiles.TryGetValue(fn, out var instFile))
            {
                if (instFile.RealName != null || !fn.Contains("node_modules"))
                    instFile.Important = true;
            }
        }

        public void BuildHtml(bool testProj = false)
        {
            string root = _mainBuildResult.CommonSourceDirectory!;
            if (!testProj && !_project.NoHtml && _project.ExampleSources.Count > 0)
            {
                if (_project.ExampleSources.Count == 1)
                {
                    BuildFastBundlerIndexHtml(
                        PathUtils.WithoutExtension(PathUtils.Subtract(_project.ExampleSources[0], root)), _cssLink,
                        "bundleExample");
                }
                else
                {
                    var htmlList = new List<string>();
                    foreach (var exampleSrc in _project.ExampleSources)
                    {
                        var moduleNameWoExt = PathUtils.WithoutExtension(PathUtils.Subtract(exampleSrc, root));
                        var justName = PathUtils.GetFile(moduleNameWoExt);
                        BuildFastBundlerIndexHtml(moduleNameWoExt, _cssLink, justName);
                        _mainBuildResult.FilesContent.GetOrAddValueRef(justName + ".html") = _indexHtml;
                        htmlList.Add(justName);
                    }

                    BuildExampleListHtml(htmlList, _cssLink);
                }
            }
            else if (testProj)
            {
                BuildFastBundlerTestHtml(_project.TestSources, root, _cssLink);
            }
            else if (!_project.NoHtml && _project.MainFile != null)
            {
                BuildFastBundlerIndexHtml(PathUtils.WithoutExtension(PathUtils.Subtract(_project.MainFile, root)),
                    _cssLink, "bundleMain");
            }

            if (testProj)
            {
                _mainBuildResult.FilesContent.GetOrAddValueRef("test.html") = _indexHtml;
                _mainBuildResult.FilesContent.GetOrAddValueRef(_versionDirPrefix + "jasmine-core.js") =
                    _tools.JasmineCoreJs;
                _mainBuildResult.FilesContent.GetOrAddValueRef(_versionDirPrefix + "jasmine-boot.js") =
                    _tools.JasmineBootJs;
            }
            else if (!_project.NoHtml)
            {
                _mainBuildResult.FilesContent.GetOrAddValueRef("index.html") = _indexHtml;
                if (_project.LiveReloadEnabled)
                {
                    _mainBuildResult.FilesContent.GetOrAddValueRef(_versionDirPrefix + "liveReload.js") =
                        _tools.LiveReloadJs.Replace("##Idx##", (_project.LiveReloadIdx + 1).ToString());
                }
            }
        }

        void BuildFastBundlerTestHtml(IEnumerable<string> testSources, string root, string cssLink)
        {
            var reqSpec = RequireBobril() + string.Join(' ',
                testSources.Where(src => !src.EndsWith(".d.ts")).Select(src =>
                {
                    var name = PathUtils.WithoutExtension(PathUtils.Subtract(src, root));
                    return $"R.r('./{name}');\n";
                }));
            _mainBuildResult.FilesContent.GetOrAddValueRef(_versionDirPrefix + "bundleTest.js") = reqSpec;
            _indexHtml = $@"<!DOCTYPE html>
<html>
    <head>
        <meta charset=""utf-8"">{_project.ExpandHtmlHead(_buildResult)}
        <title>{_project.Title}</title>{cssLink}
    </head>
    <body>
        <script src=""{_versionDirPrefix}jasmine-core.js"" charset=""utf-8""></script>
        <script src=""{_versionDirPrefix}jasmine-boot.js"" charset=""utf-8""></script>
        <script src=""{_buildResult.BundleJsUrl}"" charset=""utf-8""></script>{ImportBundle2()}
        <script src=""{_versionDirPrefix}bundleTest.js"" charset=""utf-8""></script>        
    </body>
</html>
";
        }

        void BuildFastBundlerIndexHtml(string mainModule, string cssLink, string mainRequireJs)
        {
            var liveReloadInclude = "";
            if (_project.LiveReloadEnabled)
            {
                liveReloadInclude = $@"<script src=""{_versionDirPrefix}liveReload.js"" charset=""utf-8""></script>";
            }

            _mainBuildResult.FilesContent.GetOrAddValueRef(_versionDirPrefix + mainRequireJs + ".js") =
                RequireBobril() + $"R.r('./{mainModule}');";

            _indexHtml = $@"<!DOCTYPE html>
<html>
    <head>
        <meta charset=""utf-8"">{_project.ExpandHtmlHead(_buildResult)}
        <title>{_project.Title}</title>{cssLink}
    </head>
    <body>{liveReloadInclude}
        <script src=""{_buildResult.BundleJsUrl}"" charset=""utf-8""></script>{ImportBundle2()}
        <script src=""{_versionDirPrefix}{mainRequireJs}.js"" charset=""utf-8""></script>        
    </body>
</html>
";
        }

        string ImportBundle2()
        {
            var importBundle2 = "";
            if (_bundle2Js != null)
            {
                importBundle2 = $@"<script src=""{_versionDirPrefix}bundle2.js"" charset=""utf-8""></script>";
            }

            return importBundle2;
        }

        void BuildExampleListHtml(List<string> namesWOExt, string cssLink)
        {
            var testList = "";
            for (var i = 0; i < namesWOExt.Count; i++)
            {
                testList += $@"<li><a href=""{namesWOExt[i]}.html"">{namesWOExt[i]}</a></li>";
            }

            _indexHtml = $@"<!DOCTYPE html>
<html>
    <head>
        <meta charset=""utf-8"">{_project.ExpandHtmlHead(_buildResult)}
        <title>${_project.Title}</title>${cssLink}
    </head>
    <body>
    <ul>{testList}</ul>
    </body>
</html>";
        }

        string GetGlobalDefines()
        {
            var res = new StringBuilder();
            foreach (var def in _project.BuildDefines(_mainBuildResult))
            {
                var val = TypeConverter.ToAst(def.Value).PrintToString();
                res.Append($"var {def.Key} = {val};");
            }

            return res.ToString();
        }

        string GetModuleMap()
        {
            var root = _mainBuildResult.CommonSourceDirectory;
            var res = new Dictionary<string, string>();
            foreach (var source in _buildResult.Modules)
            {
                if (!source.Value.Valid) continue;
                res.TryAdd(source.Key.ToLowerInvariant(),
                    PathUtils.Subtract(
                        PathUtils.Join(source.Value.Owner.FullPath, PathUtils.WithoutExtension(source.Value.MainFile)),
                        root));
                res.TryAdd(source.Key.ToLowerInvariant() + "/",
                    PathUtils.Subtract(PathUtils.WithoutExtension(source.Value.Owner.FullPath), root));
                var browserResolve = source.Value.ProjectOptions?.BrowserResolve;
                if (browserResolve != null)
                {
                    foreach (var (key, value) in browserResolve)
                    {
                        if (key.StartsWith('.'))
                        {
                            if (value == null)
                            {
                                res.TryAdd(
                                    PathUtils.Subtract(
                                            PathUtils.Join(source.Value.Owner.FullPath,
                                                PathUtils.WithoutExtension(key)),
                                            root)
                                        .ToLowerInvariant(), "<empty>");
                            }
                            else
                            {
                                res.TryAdd(
                                    PathUtils.Subtract(
                                            PathUtils.Join(source.Value.Owner.FullPath,
                                                PathUtils.WithoutExtension(key)),
                                            root)
                                        .ToLowerInvariant(),
                                    PathUtils.Subtract(
                                        PathUtils.Join(source.Value.Owner.FullPath, PathUtils.WithoutExtension(value)),
                                        root));
                            }
                        }
                        else
                        {
                            if (value == null)
                            {
                                res.TryAdd(key.ToLowerInvariant(), "<empty>");
                            }
                            else
                            {
                                res.TryAdd(key.ToLowerInvariant(),
                                    PathUtils.Subtract(
                                        PathUtils.Join(source.Value.Owner.FullPath, PathUtils.WithoutExtension(value)),
                                        root));
                            }
                        }
                    }
                }
            }

            return $"R.map = {Newtonsoft.Json.JsonConvert.SerializeObject(res)};";
        }

        // Bobril must be first because it contains polyfills
        string RequireBobril()
        {
            if (_buildResult.Modules.ContainsKey("bobril"))
            {
                return "R.r('bobril');";
            }

            return "";
        }
    }

    public class SourceReader : ITextFileReader
    {
        readonly IDiskCache _diskCache;
        readonly string _root;

        public SourceReader(IDiskCache diskCache, string root)
        {
            _diskCache = diskCache;
            _root = root;
        }

        public ReadOnlySpan<byte> ReadUtf8(string fileName)
        {
            var item = _diskCache.TryGetItem(PathUtils.Join(_root, fileName));
            if (item.IsFile && !item.IsInvalid)
            {
                return ((IFileCache) item).ByteContent;
            }

            return new ReadOnlySpan<byte>();
        }
    }
}
