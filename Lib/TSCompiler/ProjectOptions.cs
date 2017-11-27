using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Lib.DiskCache;
using Lib.Utils;

namespace Lib.TSCompiler
{
    public enum StyleDefNamingStyle
    {
        AddNames,
        PreserveNames,
        RemoveNames
    }

    public class ProjectOptions
    {
        public ToolsDir.IToolsDir Tools { get; set; }
        public TSProject Owner { get; set; }
        public string TestSourcesRegExp { get; set; }
        public Dictionary<string, bool> Defines { get; set; }
        public string Title { get; set; }
        public string HtmlHead { get; set; }
        public StyleDefNamingStyle StyleDefNaming { get; set; }
        public string PrefixStyleNames { get; set; }
        public string Example { get; set; }
        public bool BobrilJsx { get; set; }

        public string HtmlHeadExpanded { get; set; }
        public string MainFile { get; set; }
        public string JasmineDts { get; set; }
        public List<string> TestSources { get; set; }
        public List<string> ExampleSources { get; set; }
        public string BobrilJsxDts { get; set; }
        public FastBundleBundler MainProjFastBundle { get; set; }
        public FastBundleBundler TestProjFastBundle { get; internal set; }
        public bool LiveReloadEnabled { get; internal set; }
        public bool Localize;
        public string DefaultLanguage;
        public string OutputSubDir;

        public Translation.TranslationDb TranslationDb;

        // value could be string or byte[]
        public Dictionary<string, object> FilesContent;
        internal TSCompilerOptions CompilerOptions;

        public void RefreshMainFile()
        {
            MainFile = PathUtils.Join(Owner.Owner.FullPath, Owner.MainFile);
        }

        public void DetectBobrilJsxDts()
        {
            if (!BobrilJsx)
            {
                BobrilJsxDts = null;
                return;
            }
            var item = Owner.DiskCache.TryGetItem(PathUtils.Join(Owner.Owner.FullPath, "node_modules/bobril/jsx.d.ts"));
            if (item is IFileCache)
            {
                BobrilJsxDts = item.FullPath;
            }
            else
            {
                BobrilJsx = false;
            }
        }

        public void RefreshExampleSources()
        {
            var res = new List<string>(ExampleSources?.Count ?? 1);
            if (Example == "")
            {
                var item = (Owner.Owner.TryGetChild("example.ts") ?? Owner.Owner.TryGetChild("example.tsx")) as IFileCache;
                if (item != null)
                {
                    res.Add(item.FullPath);
                }
            }
            else
            {
                var examplePath = PathUtils.Join(Owner.Owner.FullPath, Example);
                var item = Owner.DiskCache.TryGetItem(examplePath);
                if (item is IDirectoryCache)
                {
                    Owner.DiskCache.UpdateIfNeeded(item);
                    foreach(var child in (IDirectoryCache)item)
                    {
                        if (!(child is IFileCache)) continue;
                        if (child.IsInvalid) continue;
                        res.Add(item.FullPath);
                    }
                }
                else if (item is IFileCache)
                {
                    res.Add(item.FullPath);
                }
            }
            ExampleSources = res;
        }

        public void RefreshTestSources()
        {
            JasmineDts = Tools.JasmineDtsPath;
            var res = new List<string>(TestSources?.Count ?? 4);
            var fileRegex = new Regex(TestSourcesRegExp, RegexOptions.CultureInvariant);
            RecursiveFileSearch(Owner.Owner, Owner.DiskCache, fileRegex, res);
            TestSources = res;
        }

        void RecursiveFileSearch(IDirectoryCache owner, IDiskCache diskCache, Regex fileRegex, List<string> res)
        {
            diskCache.UpdateIfNeeded(owner);
            if (owner.IsInvalid) return;
            foreach (var item in owner)
            {
                if (item is IDirectoryCache)
                {
                    if (item.Name == "node_modules") continue;
                    if (item.IsInvalid) continue;
                    RecursiveFileSearch(item as IDirectoryCache, diskCache, fileRegex, res);
                }
                else if (item is IFileCache)
                {
                    if (fileRegex.IsMatch(item.Name))
                    {
                        res.Add(item.FullPath);
                    }
                }
            }
        }
    }
}
