using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Lib.DiskCache;

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
        public TSProject Owner { get; set; }
        public string TestSourcesRegExp { get; set; }
        public Dictionary<string, bool> Defines { get; set; }
        public string Title { get; set; }
        public string HtmlHead { get; set; }
        public StyleDefNamingStyle StyleDefNaming { get; set; }
        public string PrefixStyleNames { get; set; }

        public string HtmlHeadExpanded { get; set; }
        public List<string> TestSources { get; set; }
        public FastBundleBundler FastBundle { get; set; }

        public void RefreshTestSources()
        {
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
