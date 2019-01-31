using System.Collections.Generic;
using System.Text;
using Lib.Utils;
using Lib.ToolsDir;
using System.Linq;
using System.Globalization;

namespace Lib.TSCompiler
{
    public class FastBundleBundler
    {
        SourceMap _sourceMap;
        string _sourceMapString;
        string _bundleJs;
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
        public Dictionary<string, object> FilesContent;
        string _bundlePng;
        List<float> _bundlePngInfo;

        public void Build(string sourceRoot, string mapUrl, bool testProj = false)
        {
            _versionDirPrefix = "";
            if (Project.OutputSubDir != null)
                _versionDirPrefix = Project.OutputSubDir + "/";
            var root = Project.CommonSourceDirectory;
            if (root == null)
                return;
            var sourceMapBuilder = new SourceMapBuilder();
            if (!testProj && Project.NoHtml)
            {
                sourceMapBuilder.AddText(GetInitG11nCode());
                sourceMapBuilder.AddText(_tools.LoaderJs);
                sourceMapBuilder.AddText(GetGlobalDefines());
                sourceMapBuilder.AddText(GetModuleMap());
            }

            sourceMapBuilder.AddText(_tools.TsLibSource);
            var cssLink = "";
            foreach (var source in BuildResult.Path2FileInfo)
            {
                if (source.Value.Type == FileCompilationType.JavaScriptAsset)
                {
                    sourceMapBuilder.AddSource(source.Value.Output, source.Value.MapLink);
                }
            }

            foreach (var source in BuildResult.Path2FileInfo)
            {
                if (source.Value.Type == FileCompilationType.TypeScript ||
                    source.Value.Type == FileCompilationType.JavaScript)
                {
                    if (source.Value.Output == null)
                        continue; // Skip d.ts
                    sourceMapBuilder.AddText(
                        $"R('{PathUtils.Subtract(PathUtils.WithoutExtension(source.Key), root)}',function(require, module, exports, global){{");
                    sourceMapBuilder.AddSource(source.Value.Output, source.Value.MapLink);
                    sourceMapBuilder.AddText("});");
                }
                else if (source.Value.Type == FileCompilationType.Json)
                {
                    sourceMapBuilder.AddText(
                        $"R('{PathUtils.Subtract(source.Key, root)}',function(require, module, exports, global){{");
                    sourceMapBuilder.AddText("Object.assign(exports, " + source.Value.Owner.Utf8Content + ");");
                    sourceMapBuilder.AddText("});");
                }
                else if (source.Value.Type == FileCompilationType.Css)
                {
                    string cssPath = source.Value.OutputUrl;
                    FilesContent[cssPath] = source.Value.Output;
                    cssLink += "<link rel=\"stylesheet\" href=\"" + cssPath + "\">";
                }
                else if (source.Value.Type == FileCompilationType.Resource)
                {
                    FilesContent[source.Value.OutputUrl] = source.Value.Owner.ByteContent;
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
                        FilesContent[PathUtils.InjectQuality(_bundlePng, slice.Quality)] = slice.Content;
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

            sourceMapBuilder.AddText("//# sourceMappingURL=" + mapUrl);
            _sourceMap = sourceMapBuilder.Build(root, sourceRoot);
            _sourceMapString = _sourceMap.ToString();
            _bundleJs = sourceMapBuilder.Content();
            if (!testProj && !Project.NoHtml && Project.ExampleSources.Count > 0)
            {
                if (Project.ExampleSources.Count == 1)
                {
                    BuildFastBundlerIndexHtml(
                        PathUtils.WithoutExtension(PathUtils.Subtract(Project.ExampleSources[0], root)), cssLink);
                }
                else
                {
                    var htmlList = new List<string>();
                    foreach (var exampleSrc in Project.ExampleSources)
                    {
                        var moduleNameWOExt = PathUtils.WithoutExtension(PathUtils.Subtract(exampleSrc, root));
                        BuildFastBundlerIndexHtml(moduleNameWOExt, cssLink);
                        var justName = PathUtils.SplitDirAndFile(moduleNameWOExt).Item2;
                        FilesContent[justName + ".html"] = _indexHtml;
                        htmlList.Add(justName);
                    }

                    BuildExampleListHtml(htmlList, cssLink);
                }
            }
            else if (testProj)
            {
                BuildFastBundlerTestHtml(Project.TestSources, root, cssLink);
            }
            else if (!Project.NoHtml)
            {
                BuildFastBundlerIndexHtml(PathUtils.WithoutExtension(PathUtils.Subtract(Project.MainFile, root)),
                    cssLink);
            }

            if (testProj)
            {
                FilesContent["test.html"] = _indexHtml;
                FilesContent[_versionDirPrefix + "jasmine-core.js"] = _tools.JasmineCoreJs;
                FilesContent[_versionDirPrefix + "jasmine-boot.js"] = _tools.JasmineBootJs;
                FilesContent[_versionDirPrefix + "loader.js"] = _tools.LoaderJs;
            }
            else if (!Project.NoHtml)
            {
                FilesContent["index.html"] = _indexHtml;
                FilesContent[_versionDirPrefix + "loader.js"] = _tools.LoaderJs;
                if (Project.LiveReloadEnabled)
                {
                    FilesContent[_versionDirPrefix + "liveReload.js"] =
                        _tools.LiveReloadJs.Replace("##Idx##", (Project.LiveReloadIdx + 1).ToString());
                }
            }

            FilesContent[_versionDirPrefix + PathUtils.WithoutExtension(mapUrl)] = _bundleJs;
            FilesContent[_versionDirPrefix + mapUrl] = _sourceMapString;
            BuildResult.SourceMap = _sourceMap;
        }

        void BuildFastBundlerTestHtml(IEnumerable<string> testSources, string root, string cssLink)
        {
            var reqSpec = string.Join(' ',
                testSources.Where(src => !src.EndsWith(".d.ts")).Select(src =>
                {
                    var name = PathUtils.WithoutExtension(PathUtils.Subtract(src, root));
                    return $"R.r('{name}');\n";
                }));
            _indexHtml = $@"<!DOCTYPE html>
<html>
    <head>
        <meta charset=""utf-8"">{Project.HtmlHeadExpanded}
        <title>{Project.Title}</title>{cssLink}
    </head>
    <body>{InitG11n()}
        <script src=""{_versionDirPrefix}jasmine-core.js"" charset=""utf-8""></script>
        <script src=""{_versionDirPrefix}jasmine-boot.js"" charset=""utf-8""></script>
        <script src=""{_versionDirPrefix}loader.js"" charset=""utf-8""></script>
        <script>
            {GetGlobalDefines()}
            {GetModuleMap()}
        </script>
        <script src=""{_versionDirPrefix}testbundle.js"" charset=""utf-8""></script>
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
    <body>{InitG11n()}
        <script src=""{_versionDirPrefix}loader.js"" charset=""utf-8""></script>{liveReloadInclude}
        <script>
            {GetGlobalDefines()}
            {GetModuleMap()}
        </script>
        <script src=""{_versionDirPrefix}bundle.js"" charset=""utf-8""></script>
        <script>
            {RequireBobril()}
            R.r('{mainModule}');
        </script>
    </body>
</html>
";
        }

        string GetInitG11nCode()
        {
            var res = new StringBuilder();
            if (Project.Localize)
            {
                Project.TranslationDb.BuildTranslationJs(_tools, FilesContent, Project.OutputSubDir);
                res.Append(
                    $"function g11nPath(s){{return\"./{(Project.OutputSubDir != null ? (Project.OutputSubDir + "/") : "")}\"+s.toLowerCase()+\".js\"}};");
                if (Project.DefaultLanguage != null)
                {
                    res.Append($"var g11nLoc=\"{Project.DefaultLanguage}\";");
                }
            }

            if (_bundlePng != null)
            {
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
            }

            return res.ToString();
        }

        string InitG11n()
        {
            if (!Project.Localize && _bundlePng == null)
                return "";
            var res = $"<script>{GetInitG11nCode()}</script>";
            return res;
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
            foreach (var def in Project.Defines)
            {
                var val = def.Value ? "true" : "false";
                res.Append($"var {def.Key} = {val};");
            }

            return res.ToString();
        }

        string GetModuleMap()
        {
            var root = Project.CommonSourceDirectory;
            var res = new Dictionary<string, string>();
            foreach (var source in BuildResult.Path2FileInfo)
            {
                var module = source.Value.ImportedAsModule;
                if (module != null)
                {
                    res.Add(module.ToLowerInvariant(),
                        PathUtils.Subtract(PathUtils.WithoutExtension(source.Key), root));
                }
            }

            return $"R.map = {Newtonsoft.Json.JsonConvert.SerializeObject(res)};";
        }

        // Bobril must be first because it contains polyfills
        string RequireBobril()
        {
            if (BuildResult.Path2FileInfo.ContainsKey(Project.Owner.Owner.FullPath + "/node_modules/bobril/index.ts"))
            {
                return "R.r('bobril');";
            }

            return "";
        }
    }
}