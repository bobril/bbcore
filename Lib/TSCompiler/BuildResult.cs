using System;
using System.Collections.Generic;
using Lib.DiskCache;
using Lib.Utils;

namespace Lib.TSCompiler
{
    public class BuildResult
    {
        public BuildResult()
        {
            Path2FileInfo = new Dictionary<string, TSFileAdditionalInfo>();
            RecompiledLast = new HashSet<TSFileAdditionalInfo>();
            Modules = new Dictionary<string, TSProject>();
        }

        public Dictionary<string, TSFileAdditionalInfo> Path2FileInfo;
        public HashSet<TSFileAdditionalInfo> RecompiledLast;
        public Dictionary<string, TSProject> Modules;

        public SourceMap SourceMap { get; internal set; }
    }
}
