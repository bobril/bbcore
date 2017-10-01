using System.Collections.Generic;
using Lib.Utils;

namespace Lib.TSCompiler
{
    public class BuildResult
    {
        public BuildResult()
        {
            Path2FileInfo = new Dictionary<string, TSFileAdditionalInfo>();
            RecompiledLast = new HashSet<TSFileAdditionalInfo>();
        }

        public Dictionary<string, TSFileAdditionalInfo> Path2FileInfo;
        public HashSet<TSFileAdditionalInfo> RecompiledLast;

        public SourceMap SourceMap { get; internal set; }
    }
}
