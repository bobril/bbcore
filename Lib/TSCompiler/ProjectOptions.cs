using System.Collections.Generic;

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
    }
}
