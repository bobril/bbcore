using System.Collections.Generic;
using System.Text;
using Lib.Utils;
using Lib.ToolsDir;
using System;
using System.Linq;

namespace Lib.TSCompiler
{
    public class FastBundleBundler
    {
        static string tslibSource;
        SourceMap _sourceMap;
        string _sourceMapString;
        string _bundleJs;
        string _indexHtml;
        readonly IToolsDir _tools;

        static FastBundleBundler()
        {
            tslibSource = ResourceUtils.GetText("Lib.TSCompiler.tslib.js");
        }

        public FastBundleBundler(IToolsDir tools)
        {
            _tools = tools;
        }

        public ProjectOptions Project;
        public BuildResult BuildResult;

        // value could be string or byte[]
        public Dictionary<string, object> FilesContent;


        public void Build(string sourceRoot, string mapUrl, bool testProj = false)
        {
            var diskCache = Project.Owner.DiskCache;
            var root = Project.Owner.Owner.FullPath;
            var sourceMapBuilder = new SourceMapBuilder();
            sourceMapBuilder.AddText(tslibSource);
            var cssLink = "";
            foreach (var source in BuildResult.Path2FileInfo)
            {
                if (source.Value.Type == FileCompilationType.TypeScript || source.Value.Type == FileCompilationType.JavaScript)
                {
                    if (source.Value.Output == null) continue; // Skip d.ts
                    sourceMapBuilder.AddText($"R('{PathUtils.Subtract(PathUtils.WithoutExtension(source.Key), root)}',function(require, module, exports, global){{");
                    sourceMapBuilder.AddSource(source.Value.Output, source.Value.MapLink);
                    sourceMapBuilder.AddText("});");
                }
                else if (source.Value.Type == FileCompilationType.Css)
                {
                    string cssPath = PathUtils.Subtract(source.Value.Owner.FullPath, root);
                    FilesContent[cssPath] = source.Value.Owner.ByteContent;
                    cssLink += "<link rel=\"stylesheet\" href=\"" + cssPath + "\">";
                }
                else if (source.Value.Type == FileCompilationType.Resource)
                {
                    FilesContent[PathUtils.Subtract(source.Value.Owner.FullPath, root)] = source.Value.Owner.ByteContent;
                }
            }
            sourceMapBuilder.AddText("//# sourceMappingURL=" + mapUrl);
            _sourceMap = sourceMapBuilder.Build(root, sourceRoot);
            _sourceMapString = _sourceMap.ToString();
            _bundleJs = sourceMapBuilder.Content();
            if (!testProj && Project.ExampleSources.Count > 0)
            {
                if (Project.ExampleSources.Count == 1)
                {
                    BuildFastBundlerIndexHtml(PathUtils.WithoutExtension(PathUtils.Subtract(Project.ExampleSources[0], root)), cssLink);
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
            else
            {
                BuildFastBundlerIndexHtml(PathUtils.WithoutExtension(PathUtils.Subtract(Project.MainFile, root)), cssLink);
            }
            if (testProj)
            {
                FilesContent["test.html"] = _indexHtml;
                FilesContent["jasmine-core.js"] = _tools.JasmineCoreJs;
                FilesContent["jasmine-boot.js"] = _tools.JasmineBootJs;
            }
            else
            {
                FilesContent["index.html"] = _indexHtml;
                FilesContent["loader.js"] = _tools.LoaderJs;
            }
            FilesContent[PathUtils.WithoutExtension(mapUrl)] = _bundleJs;
            FilesContent[mapUrl] = _sourceMapString;
            BuildResult.SourceMap = _sourceMap;
        }

        void BuildFastBundlerTestHtml(List<string> testSources, string root, string cssLink)
        {
            var reqSpec = string.Join(' ', testSources.Select(src => "R.r(\'" + PathUtils.WithoutExtension(PathUtils.Subtract(src, root)) + "\');"));
            _indexHtml = $@"<!DOCTYPE html>
<html>
    <head>
        <meta charset=""utf-8"">{Project.HtmlHeadExpanded}
        <title>{Project.Title}</title>{cssLink}
    </head>
    <body>{InitG11n()}
        <script type=""text/javascript"" src=""jasmine-core.js"" charset=""utf-8""></script>
        <script type=""text/javascript"" src=""jasmine-boot.js"" charset=""utf-8""></script>
        <script type=""text/javascript"" src=""loader.js"" charset=""utf-8""></script>
        <script type=""text/javascript"">
            {GetGlobalDefines()}
            {GetModuleMap()}
        </script>
        <script type=""text/javascript"" src=""testbundle.js"" charset=""utf-8""></script>
        <script type=""text/javascript"">
            {RequireBobril()} {reqSpec}
        </script>
    </body>
</html>
";
        }

        void BuildFastBundlerIndexHtml(string mainModule, string cssLink)
        {
            _indexHtml = $@"<!DOCTYPE html>
<html>
    <head>
        <meta charset=""utf-8"">{Project.HtmlHeadExpanded}
        <title>{Project.Title}</title>{cssLink}
    </head>
    <body>{InitG11n()}
        <script type=""text/javascript"" src=""loader.js"" charset=""utf-8""></script>
        <script type=""text/javascript"">
            {GetGlobalDefines()}
            {GetModuleMap()}
        </script>
        <script type=""text/javascript"" src=""bundle.js"" charset=""utf-8""></script>
        <script type=""text/javascript"">
            {RequireBobril()}
            R.r('{mainModule}');
        </script>
    </body>
</html>
";
        }

        string InitG11n()
        {
            if (!Project.Localize)
                return "";
            Project.TranslationDb.BuildTranslationJs(_tools, FilesContent);
            var res = "<script>";
            if (Project.Localize)
            {
                res += $"function g11nPath(s){{return\"./{(Project.OutputSubDir != null ? (Project.OutputSubDir + "/") : "")}\"+s.toLowerCase()+\".js\"}};";
                if (Project.DefaultLanguage != null)
                {
                    res += $"var g11nLoc=\"{Project.DefaultLanguage}\";";
                }
            }
            //if (Project.bundlePng)
            //{
            //    res += $"var bobrilBPath=\"{Project.bundlePng}\";";
            //}
            res += "</script>";
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
            var res = new StringBuilder();
            if (Project.Defines != null) foreach (var def in Project.Defines)
                {
                    var val = def.Value ? "true" : "false";
                    res.Append($"var {def.Key} = {val};");
                }
            return res.ToString();
        }

        string GetModuleMap()
        {
            var root = Project.Owner.Owner.FullPath;
            var res = new Dictionary<string, string>();
            foreach (var source in BuildResult.Path2FileInfo)
            {
                var module = source.Value.ImportedAsModule;
                if (module != null)
                {
                    res.Add(module.ToLowerInvariant(), PathUtils.Subtract(PathUtils.WithoutExtension(source.Key), root));
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
