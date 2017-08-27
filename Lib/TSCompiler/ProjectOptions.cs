using System.Collections.Generic;

namespace Lib.TSCompiler
{
    public class ProjectOptions
    {
        public TSProject Owner { get; set; }
        public string TestSourcesRegExp { get; set; }
        public Dictionary<string, bool> Defines { get; set; }

        public List<string> TestSources { get; set; }
        public FastBundleBundler FastBundle { get; set; }
    }
}
