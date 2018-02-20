using System.Collections.Generic;
using Lib.Utils;
using Lib.ToolsDir;
using Lib.Bundler;
using System.Linq;
using System.IO;

namespace Lib.TSCompiler
{
    public class BundleBundler : IBundlerCallback
    {
        string _mainJsBundleUrl;
        string _bundlePng;
        string _indexHtml;
        readonly IToolsDir _tools;

        public BundleBundler(IToolsDir tools)
        {
            _tools = tools;
        }

        public ProjectOptions Project;
        public BuildResult BuildResult;

        // value could be string or byte[] or Lazy<string|byte[]>
        public Dictionary<string, object> FilesContent;
        Dictionary<string, string> _jsFilesContent;

        public void Build(bool compress, bool mangle, bool beautify)
        {
            var diskCache = Project.Owner.DiskCache;
            var root = Project.Owner.Owner.FullPath;
            var cssLink = "";
            _jsFilesContent = new Dictionary<string, string>();
            foreach (var source in BuildResult.Path2FileInfo)
            {
                if (source.Value.Type == FileCompilationType.TypeScript || source.Value.Type == FileCompilationType.JavaScript)
                {
                    if (source.Value.Output == null)
                        continue; // Skip d.ts
                    _jsFilesContent[PathUtils.ChangeExtension(source.Key, "js")] = source.Value.Output;
                }
                else if (source.Value.Type == FileCompilationType.Css)
                {
                    string cssPath = Project.AllocateName(".css");
                    FilesContent[cssPath] = source.Value.Owner.ByteContent;
                    cssLink += "<link rel=\"stylesheet\" href=\"" + cssPath + "\">";
                }
                else if (source.Value.Type == FileCompilationType.Resource)
                {
                    FilesContent[PathUtils.Subtract(source.Value.Owner.FullPath, root)] = source.Value.Owner.ByteContent;
                }
            }
            if (Project.SpriteGeneration)
            {
                _bundlePng = Project.BundlePngUrl;
                FilesContent[_bundlePng] = Project.SpriteGenerator.BuildImage(true);
            }
            var bundler = new BundlerImpl(_tools);
            bundler.Callbacks = this;
            if (Project.ExampleSources.Count > 0)
            {
                bundler.MainFiles = new[] { PathUtils.ChangeExtension(Project.ExampleSources[0], "js") };
            }
            else
            {
                bundler.MainFiles = new[] { PathUtils.ChangeExtension(Project.MainFile, "js") };
            }
            _mainJsBundleUrl = Project.BundleJsUrl;
            bundler.Compress = compress;
            bundler.Mangle = mangle;
            bundler.Beautify = beautify;
            var defines = new Dictionary<string, object>();
            foreach (var p in Project.Defines)
            {
                defines.Add(p.Key, p.Value);
            }
            bundler.Defines = defines;
            bundler.Bundle();
            BuildFastBundlerIndexHtml(cssLink);
            FilesContent["index.html"] = _indexHtml;
        }

        void BuildFastBundlerIndexHtml(string cssLink)
        {
            _indexHtml = $@"<!DOCTYPE html><html><head><meta charset=""utf-8"">{Project.HtmlHeadExpanded}<title>{Project.Title}</title>{cssLink}</head><body>{InitG11n()}<script type=""text/javascript"" src=""{_mainJsBundleUrl}"" charset=""utf-8""></script></body></html>";
        }

        string InitG11n()
        {
            if (!Project.Localize && _bundlePng == null)
                return "";
            var res = "<script>";
            if (Project.Localize)
            {
                Project.TranslationDb.BuildTranslationJs(_tools, FilesContent);
                res += $"function g11nPath(s){{return\"./{(Project.OutputSubDir != null ? (Project.OutputSubDir + "/") : "")}\"+s.toLowerCase()+\".js\"}};";
                if (Project.DefaultLanguage != null)
                {
                    res += $"var g11nLoc=\"{Project.DefaultLanguage}\";";
                }
            }
            if (_bundlePng != null)
            {
                res += $"var bobrilBPath=\"{_bundlePng}\";";
            }
            res += "</script>";
            return res;
        }

        public string ReadContent(string name)
        {
            if (_jsFilesContent.TryGetValue(name, out var content))
            {
                return content;
            }
            throw new System.InvalidOperationException("Bundler Read Content does not exists:" + name);
        }

        public void WriteBundle(string name, string content)
        {
            FilesContent[name] = content;
        }

        public string GenerateBundleName(string forName)
        {
            if (forName == "")
                return _mainJsBundleUrl;
            return Project.AllocateName(forName.Replace("/", "_") + ".js");
        }

        public string ResolveRequire(string name, string from)
        {
            if (name.StartsWith("./") || name.StartsWith("../"))
            {
                return PathUtils.Join(PathUtils.Parent(from), name) + ".js";
            }
            var diskCache = Project.Owner.DiskCache;
            var moduleInfo = TSProject.FindInfoForModule(Project.Owner.Owner, diskCache, name, out var diskName);
            if (moduleInfo == null)
                return null;
            var mainFile = PathUtils.ChangeExtension(PathUtils.Join(moduleInfo.Owner.FullPath, moduleInfo.MainFile), "js");
            return mainFile;
        }

        public string TslibSource(bool withImport)
        {
            return _tools.TsLibSource + (withImport ? _tools.ImportSource : "");
        }
    }
}
