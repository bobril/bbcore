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
            foreach (var source in BuildResult.WithoutExtension2Source)
            {
                sourceMapBuilder.AddText($"R('{PathUtils.Subtract(source.Key, root)}',function(require, module, exports, global){{");
                sourceMapBuilder.AddSource(source.Value.JsOutput, source.Value.MapLink);
                sourceMapBuilder.AddText("});");
            }
            sourceMapBuilder.AddText("//# sourceMappingURL=" + mapUrl);
            _sourceMap = sourceMapBuilder.Build(root, sourceRoot);
            _sourceMapString = _sourceMap.ToString();
            _bundleJs = sourceMapBuilder.Content();
            BuildFastBundlerIndexHtml();
            BuildResult.FilesContent.Clear();
            BuildResult.FilesContent["index.html"] = _indexHtml;
            BuildResult.FilesContent["loader.js"] = _tools.LoaderJs;
            BuildResult.FilesContent["bundle.js"] = _bundleJs;
            BuildResult.FilesContent["bundle.js.map"] = _sourceMapString;
            foreach (var source in BuildResult.WithoutExtension2Source)
            {
                var sourceInfo = source.Value.SourceInfo;
                if (sourceInfo == null) continue;
                var a = sourceInfo.assets;
                for (int i = 0; i < a.Count; i++)
                {
                    var name = a[i].name;
                    if (name == null) continue;
                    var resourceFileCache = diskCache.TryGetItem(name) as IFileCache;
                    if (resourceFileCache != null)
                    {
                        BuildResult.FilesContent[PathUtils.Subtract(name, root)] = resourceFileCache.ByteContent;
                    }
                }

            }
            BuildResult.SourceMap = _sourceMap;
        }

        void BuildFastBundlerIndexHtml()
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

        string GetModuleMap()
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

        string RequireBobril()
        {
            if (BuildResult.WithoutExtension2Source.ContainsKey(Project.Owner.Owner.FullPath + "/node_modules/bobril/index"))
            {
                return "R.r('bobril')";
            }
            return "";
        }
    }
}
