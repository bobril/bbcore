using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Lib.TSCompiler
{
    public class TSConfigJson
    {
        public TSCompilerOptions compilerOptions { get; set; }
        public List<string> files { get; set; }
        public List<string> include { get; set; }
    }
}
