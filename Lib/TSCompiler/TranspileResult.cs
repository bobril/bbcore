using System.Collections.Generic;

namespace Lib.TSCompiler
{
    public class TranspileResult
    {
        public string JavaScript;
        public string SourceMap;
        public List<Diagnostic> Diagnostics;
    }
}
