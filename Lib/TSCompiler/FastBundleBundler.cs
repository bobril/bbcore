using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Lib.Utils;

namespace Lib.TSCompiler
{
    public class FastBundleBundler
    {
        static string tslibSource;
        SourceMap _sourceMap;
        string _sourceMapString;
        string _bundleJs;
        string _indexHtml;

        static FastBundleBundler()
        {
            tslibSource = ResourceUtils.GetText("Lib.TSCompiler.tslib.js");
        }

        public ProjectOptions Project { get; set; }
        public BuildResult BuildResult { get; set; }
        public SourceMap SourceMap { get => _sourceMap; }
        public string SourceMapString { get => _sourceMapString; }
        public string BundleJs { get => _bundleJs; }
        public string IndexHtml { get => _indexHtml; }

        public void Build(string sourceRoot, string mapUrl)
        {
            var root = Project.Owner.Owner.FullPath;
            var sourceMapBuilder = new SourceMapBuilder();
            sourceMapBuilder.AddText(tslibSource);
            foreach (var source in BuildResult.WithoutExtension2Source)
            {
                sourceMapBuilder.AddText($"R('{PathUtils.Subtract(source.Key, root)}',function(require, module, exports, global){{");
                sourceMapBuilder.AddSource(source.Value.JsLink.Owner.Utf8Content, source.Value.MapLink);
                sourceMapBuilder.AddText("});");
            }
            sourceMapBuilder.AddText("//# sourceMappingURL=" + mapUrl);
            _sourceMap = sourceMapBuilder.Build();
            _sourceMap.sourceRoot = sourceRoot;
            _sourceMapString = _sourceMap.ToString();
            _bundleJs = sourceMapBuilder.Content();
            BuildFastBundlerIndexHtml();
        }

        public void BuildFastBundlerIndexHtml()
        {
            var title = "Bobril Application";
            _indexHtml = $@"<!DOCTYPE html>
<html>
    <head>
        <meta charset=""utf-8"">
        <title>{title}</title>
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

        public string GetModuleMap()
        {
            var root = Project.Owner.Owner.FullPath;
            var res = new Dictionary<string, string>();
            foreach (var source in BuildResult.WithoutExtension2Source)
            {
                var module = source.Value.ImportedAsModule;
                if (module != null)
                {
                    res.Add(module, PathUtils.Subtract(source.Key, root));
                }
            }
            return $"R.map = {Newtonsoft.Json.JsonConvert.SerializeObject(res)};";
        }

        public string RequireBobril()
        {
            if (BuildResult.WithoutExtension2Source.ContainsKey(Project.Owner.Owner.FullPath + "/node_modules/bobril/index"))
            {
                return "R.r('bobril')";
            }
            return "";
        }
    }
}
