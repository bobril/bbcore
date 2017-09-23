using System.Collections.Generic;
using Lib.Utils;

namespace Lib.TSCompiler
{

    public class BuildResult
    {
        public BuildResult()
        {
            WithoutExtension2Source = new Dictionary<string, TSFileAdditionalInfo>();
            RecompiledLast = new HashSet<TSFileAdditionalInfo>();
            FilesContent = new Dictionary<string, object>();
        }

        public Dictionary<string, TSFileAdditionalInfo> WithoutExtension2Source;
        public HashSet<TSFileAdditionalInfo> RecompiledLast;

        // value could be string or byte[]
        public Dictionary<string, object> FilesContent;

        public SourceMap SourceMap { get; internal set; }
    }
}
