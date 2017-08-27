using System.Collections.Generic;

namespace Lib.TSCompiler
{

    public class BuildResult
    {
        public BuildResult()
        {
            WithoutExtension2Source = new Dictionary<string, TSFileAdditionalInfo>();
            RecompiledLast = new HashSet<TSFileAdditionalInfo>();
        }

        public Dictionary<string, TSFileAdditionalInfo> WithoutExtension2Source;
        public HashSet<TSFileAdditionalInfo> RecompiledLast;
    }
}
