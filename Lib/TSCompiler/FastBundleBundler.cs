using System.Collections.Generic;
using System.Text;
using Lib.Utils;
using Lib.ToolsDir;
using System.Linq;
using System.Globalization;
using BTDB.Collections;
using Njsast.SourceMap;
using System;
using Njsast.Bundler;

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

        public FastBundleBundler(IToolsDir tools)
        {
            _tools = tools;
        }

        public ProjectOptions Project;
        public BuildResult BuildResult;

        // value could be string or byte[] or Lazy<string|byte[]>
        public RefDictionary<string, object> FilesContent;
        string _bundlePng;
        List<float> _bundlePngInfo;

        public Dictionary<string, SourceMap> SourceMaps;

        public void Build(string sourceRoot, bool testProj = false)
        {
            _versionDirPrefix = "";
            if (Project.OutputSubDir != null)
                _versionDirPrefix = Project.OutputSubDir + "/";
            var root = Project.CommonSourceDirectory;
            var incremental = BuildResult.Incremental;
            var start = DateTime.UtcNow;
            var sourceMapBuilder = new SourceMapBuilder();
            if (Project.Localize)
            {
                if (!incremental)
                {
                    sourceMapBuilder.AddText(
                        $"function g11nPath(s){{return\"./{(Project.OutputSubDir != null ? (Project.OutputSubDir + "/") : "")}\"+s.toLowerCase()+\".js\"}};");
                    if (Project.DefaultLanguage != null)
                    {
                        sourceMapBuilder.AddText($"var g11nLoc=\"{Project.DefaultLanguage}\";");
                    }
                }
            }

            if (_bundlePng != null && !incremental)
            {
                sourceMapBuilder.AddText(GetInitSpriteCode());
            }

            if (!incremental)
            {
                sourceMapBuilder.AddText(_tools.LoaderJs);
                if (Project.Defines != null) sourceMapBuilder.AddText(GetGlobalDefines());
                sourceMapBuilder.AddText(GetModuleMap());
                sourceMapBuilder.AddText(BundlerHelpers.JsHeaders(false));
            }

            var cssLink = "";

            var sortedResultSet = incremental ? BuildResult.RecompiledIncrementaly.OrderBy(f => f.Owner.FullPath).ToArray() : BuildResult.Path2FileInfo.Values.OrderBy(f => f.Owner.FullPath).ToArray();

            if (!incremental)
            {
                foreach (var source in BuildResult.JavaScriptAssets)
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
                    Project.ApplySourceInfo(sourceReplacer, source.SourceInfo, BuildResult);
                    sourceReplacer.Apply(adder);
                    //sourceMapBuilder.AddSource(source.Output, source.MapLink);
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
                    string cssPath = BuildResult.ToOutputUrl(source);
                    FilesContent.GetOrAddValueRef(cssPath) = source.Output;
                    cssLink += "<link rel=\"stylesheet\" href=\"" + cssPath + "\">";
                }
                else if (source.Type == FileCompilationType.Css)
                {
                    string cssPath = BuildResult.ToOutputUrl(source);
                    FilesContent.GetOrAddValueRef(cssPath) = source.Output;
                    cssLink += "<link rel=\"stylesheet\" href=\"" + cssPath + "\">";
                }
                else if (source.Type == FileCompilationType.Resource)
                {
                    FilesContent.GetOrAddValueRef(BuildResult.ToOutputUrl(source)) = source.Owner.ByteContent;
                }
            }

            if (Project.SpriteGeneration)
            {
                _bundlePng = Project.BundlePngUrl;
                var bundlePngContent = Project.SpriteGenerator.BuildImage(false);
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

            if (!testProj && Project.NoHtml)
            {
                sourceMapBuilder.AddText(RequireBobril());
                sourceMapBuilder.AddText(
                    $"R.r('{PathUtils.WithoutExtension(PathUtils.Subtract(Project.MainFile, root))}');");
            }

            if (Project.Localize)
            {
                Project.TranslationDb.BuildTranslationJs(_tools, FilesContent, Project.OutputSubDir);
            }

            if (incremental)
            {
                sourceMapBuilder.AddText("//# sourceMappingURL=bundle2.js.map");
                _sourceMap2 = sourceMapBuilder.Build(root, sourceRoot);
                _sourceMap2String = _sourceMap2.ToString();
                _bundle2Js = sourceMapBuilder.Content();
                Project.Owner.Logger.Info("JS Bundle length: " + _bundleJs.Length + " SourceMap length: " + _sourceMapString.Length + " Delta: " + _bundle2Js.Length + " SM:" + _sourceMap2String.Length + " T:" + (DateTime.UtcNow - start).TotalMilliseconds.ToString("F0") + "ms");
            }
            else
            {
                sourceMapBuilder.AddText("//# sourceMappingURL=bundle.js.map");
                _sourceMap = sourceMapBuilder.Build(root, sourceRoot);
                _sourceMapString = _sourceMap.ToString();
                _bundleJs = sourceMapBuilder.Content();
                _sourceMap2 = null;
                _sourceMap2String = null;
                _bundle2Js = null;
                _cssLink = cssLink;
                Project.Owner.Logger.Info("JS Bundle length: " + _bundleJs.Length + " SourceMap length: " + _sourceMapString.Length + " T:" + (DateTime.UtcNow - start).TotalMilliseconds.ToString("F0") + "ms");
            }
            FilesContent.GetOrAddValueRef(_versionDirPrefix + "bundle.js") = _bundleJs;
            FilesContent.GetOrAddValueRef(_versionDirPrefix + "bundle.js.map") = _sourceMapString;
            if (incremental)
            {
                FilesContent.GetOrAddValueRef(_versionDirPrefix + "bundle2.js") = _bundle2Js;
                FilesContent.GetOrAddValueRef(_versionDirPrefix + "bundle2.js.map") = _sourceMap2String;
                SourceMaps = new Dictionary<string, SourceMap>
                {
                    { "bundle.js", _sourceMap },
                    { "bundle2.js", _sourceMap2 }
                };
            }
            else
            {
                SourceMaps = new Dictionary<string, SourceMap>
                {
                    { "bundle.js", _sourceMap }
                };
            }
        }

        public void BuildHtml(bool testProj = false)
        {
            var root = Project.CommonSourceDirectory;
            if (!testProj && !Project.NoHtml && Project.ExampleSources.Count > 0)
            {
                if (Project.ExampleSources.Count == 1)
                {
                    BuildFastBundlerIndexHtml(
                        PathUtils.WithoutExtension(PathUtils.Subtract(Project.ExampleSources[0], root)), _cssLink);
                }
                else
                {
                    var htmlList = new List<string>();
                    foreach (var exampleSrc in Project.ExampleSources)
                    {
                        var moduleNameWOExt = PathUtils.WithoutExtension(PathUtils.Subtract(exampleSrc, root));
                        BuildFastBundlerIndexHtml(moduleNameWOExt, _cssLink);
                        var justName = PathUtils.GetFile(moduleNameWOExt);
                        FilesContent.GetOrAddValueRef(justName + ".html") = _indexHtml;
                        htmlList.Add(justName);
                    }

                    BuildExampleListHtml(htmlList, _cssLink);
                }
            }
            else if (testProj)
            {
                BuildFastBundlerTestHtml(Project.TestSources, root, _cssLink);
            }
            else if (!Project.NoHtml)
            {
                BuildFastBundlerIndexHtml(PathUtils.WithoutExtension(PathUtils.Subtract(Project.MainFile, root)),
                    _cssLink);
            }

            if (testProj)
            {
                FilesContent.GetOrAddValueRef("test.html") = _indexHtml;
                FilesContent.GetOrAddValueRef(_versionDirPrefix + "jasmine-core.js") = _tools.JasmineCoreJs;
                FilesContent.GetOrAddValueRef(_versionDirPrefix + "jasmine-boot.js") = _tools.JasmineBootJs;
            }
            else if (!Project.NoHtml)
            {
                FilesContent.GetOrAddValueRef("index.html") = _indexHtml;
                if (Project.LiveReloadEnabled)
                {
                    FilesContent.GetOrAddValueRef(_versionDirPrefix + "liveReload.js") =
                        _tools.LiveReloadJs.Replace("##Idx##", (Project.LiveReloadIdx + 1).ToString());
                }
            }
        }

        void BuildFastBundlerTestHtml(IEnumerable<string> testSources, string root, string cssLink)
        {
            var reqSpec = string.Join(' ',
                testSources.Where(src => !src.EndsWith(".d.ts")).Select(src =>
                {
                    var name = PathUtils.WithoutExtension(PathUtils.Subtract(src, root));
                    return $"R.r('./{name}');\n";
                }));
            _indexHtml = $@"<!DOCTYPE html>
<html>
    <head>
        <meta charset=""utf-8"">{Project.HtmlHeadExpanded}
        <title>{Project.Title}</title>{cssLink}
    </head>
    <body>
        <script src=""{_versionDirPrefix}jasmine-core.js"" charset=""utf-8""></script>
        <script src=""{_versionDirPrefix}jasmine-boot.js"" charset=""utf-8""></script>
        <script src=""{_versionDirPrefix}bundle.js"" charset=""utf-8""></script>{ImportBundle2()}
        <script>
            {RequireBobril()}
            {reqSpec}
        </script>
    </body>
</html>
";
        }

        void BuildFastBundlerIndexHtml(string mainModule, string cssLink)
        {
            var liveReloadInclude = "";
            if (Project.LiveReloadEnabled)
            {
                liveReloadInclude = $@"<script src=""{_versionDirPrefix}liveReload.js"" charset=""utf-8""></script>";
            }
            _indexHtml = $@"<!DOCTYPE html>
<html>
    <head>
        <meta charset=""utf-8"">{Project.HtmlHeadExpanded}
        <title>{Project.Title}</title>{cssLink}
    </head>
    <body>{liveReloadInclude}
        <script src=""{_versionDirPrefix}bundle.js"" charset=""utf-8""></script>{ImportBundle2()}
        <script>
            {RequireBobril()}
            R.r('./{mainModule}');
        </script>
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

        string GetInitSpriteCode()
        {
            var res = new StringBuilder();
            res.Append($"var bobrilBPath=\"{_bundlePng}\"");
            if (_bundlePngInfo.Count > 1)
            {
                res.Append($",bobrilBPath2=[");
                for (var i = 1; i < _bundlePngInfo.Count; i++)
                {
                    var q = _bundlePngInfo[i];
                    if (i > 1) res.Append(",");
                    res.Append(
                        $"[\"{PathUtils.InjectQuality(_bundlePng, q)}\",{q.ToString(CultureInfo.InvariantCulture)}]");
                }

                res.Append("]");
            }
            return res.ToString();
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
        <meta charset=""utf-8"">{Project.HtmlHeadExpanded}
        <title>${Project.Title}</title>${cssLink}
    </head>
    <body>
    <ul>{testList}</ul>
    </body>
</html>";
        }

        string GetGlobalDefines()
        {
            if (Project.Defines == null) return "";
            var res = new StringBuilder();
            foreach (var def in Project.ExpandedDefines)
            {
                var val = def.Value.PrintToString();
                res.Append($"var {def.Key} = {val};");
            }

            return res.ToString();
        }

        string GetModuleMap()
        {
            var root = Project.CommonSourceDirectory;
            var res = new Dictionary<string, string>();
            foreach (var source in BuildResult.Modules)
            {
                if (!source.Value.Valid) continue;
                res.TryAdd(source.Key.ToLowerInvariant(),
                    PathUtils.Subtract(PathUtils.Join(source.Value.Owner.FullPath, PathUtils.WithoutExtension(source.Value.MainFile)), root));
                res.TryAdd(source.Key.ToLowerInvariant() + "/",
                    PathUtils.Subtract(PathUtils.WithoutExtension(source.Value.Owner.FullPath), root));
            }

            return $"R.map = {Newtonsoft.Json.JsonConvert.SerializeObject(res)};";
        }

        // Bobril must be first because it contains polyfills
        string RequireBobril()
        {
            if (BuildResult.Modules.ContainsKey("bobril"))
            {
                return "R.r('bobril');";
            }

            return "";
        }
    }
}
