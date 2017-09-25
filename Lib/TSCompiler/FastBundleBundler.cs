using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Lib.Utils;
using Lib.ToolsDir;
using Lib.DiskCache;

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

        public ProjectOptions Project { get; set; }
        public BuildResult BuildResult { get; set; }

        public void Build(string sourceRoot, string mapUrl)
        {
            var diskCache = Project.Owner.DiskCache;
            var root = Project.Owner.Owner.FullPath;
            var sourceMapBuilder = new SourceMapBuilder();
            sourceMapBuilder.AddText(tslibSource);
            BuildResult.FilesContent.Clear();
            var cssLink = "";
            foreach (var source in BuildResult.Path2FileInfo)
            {
                if (source.Value.Type == FileCompilationType.TypeScript || source.Value.Type == FileCompilationType.JavaScript)
                {
                    sourceMapBuilder.AddText($"R('{PathUtils.Subtract(PathUtils.WithoutExtension(source.Key), root)}',function(require, module, exports, global){{");
                    sourceMapBuilder.AddSource(source.Value.Output, source.Value.MapLink);
                    sourceMapBuilder.AddText("});");
                }
                else if (source.Value.Type == FileCompilationType.Css)
                {
                    string cssPath = PathUtils.Subtract(source.Value.Owner.FullPath, root);
                    BuildResult.FilesContent[cssPath] = source.Value.Owner.ByteContent;
                    cssLink += "<link rel=\"stylesheet\" href=\"" + cssPath + "\">";
                }
                else if (source.Value.Type == FileCompilationType.Resource)
                {
                    BuildResult.FilesContent[PathUtils.Subtract(source.Value.Owner.FullPath, root)] = source.Value.Owner.ByteContent;
                }
            }
            sourceMapBuilder.AddText("//# sourceMappingURL=" + mapUrl);
            _sourceMap = sourceMapBuilder.Build(root, sourceRoot);
            _sourceMapString = _sourceMap.ToString();
            _bundleJs = sourceMapBuilder.Content();
            BuildFastBundlerIndexHtml(cssLink);
            BuildResult.FilesContent["index.html"] = _indexHtml;
            BuildResult.FilesContent["loader.js"] = _tools.LoaderJs;
            BuildResult.FilesContent["bundle.js"] = _bundleJs;
            BuildResult.FilesContent["bundle.js.map"] = _sourceMapString;
            BuildResult.SourceMap = _sourceMap;
        }

        void BuildFastBundlerIndexHtml(string cssLink)
        {
            _indexHtml = $@"<!DOCTYPE html>
<html>
    <head>
        <meta charset=""utf-8"">{Project.HtmlHeadExpanded}
        <title>{Project.Title}</title>{cssLink}
    </head>
    <body>
        <script type=""text/javascript"" src=""loader.js"" charset=""utf-8""></script>
        <script type=""text/javascript"">
            {GetGlobalDefines()}
            {GetModuleMap()}
        </script>
        <script type=""text/javascript"" src=""bundle.js"" charset=""utf-8""></script>
        <script type=""text/javascript"">
            {RequireBobril()}
            R.r('{PathUtils.WithoutExtension(Project.Owner.MainFile)}');
        </script>
    </body>
</html>
";
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
                    res.Add(module, PathUtils.Subtract(PathUtils.WithoutExtension(source.Key), root));
                }
            }
            return $"R.map = {Newtonsoft.Json.JsonConvert.SerializeObject(res)};";
        }

        // Bobril must be first because it contains polyfills
        string RequireBobril()
        {
            if (BuildResult.Path2FileInfo.ContainsKey(Project.Owner.Owner.FullPath + "/node_modules/bobril/index.ts"))
            {
                return "R.r('bobril')";
            }
            return "";
        }
    }
}
