using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Lib.TSCompiler
{
    public static class PragmaParser
    {
        // BBIgnoreObsolete: modulea, moduleb
        static Regex _bbIgnoreObsoleteRegex = new Regex("^// BBIgnoreObsolete: (.*)$",RegexOptions.Multiline|RegexOptions.Compiled);

        public static string[] ParseIgnoreImportingObsolete(string src)
        {
            var lastImportIndex = src.LastIndexOf("\nimport", StringComparison.Ordinal);
            var m = _bbIgnoreObsoleteRegex.Match(src, 0, lastImportIndex + 1);
            if (!m.Success) return Array.Empty<string>();
            return m.Groups[1].Value.Split(",").Select(s => s.Trim()).ToArray();
        }
    }
}
