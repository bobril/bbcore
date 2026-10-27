using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Lib.TSCompiler;

public static partial class PragmaParser
{
    // BBIgnoreObsolete: modulea, moduleb
    [GeneratedRegex("^// BBIgnoreObsolete: (.*)$", RegexOptions.Multiline)]
    private static partial Regex BBIgnoreObsoleteRegex { get; }

    public static string[] ParseIgnoreImportingObsolete(string src)
    {
        var lastImportIndex = src.LastIndexOf("\nimport", StringComparison.Ordinal);
        var m = BBIgnoreObsoleteRegex.Match(src, 0, lastImportIndex + 1);
        if (!m.Success) return Array.Empty<string>();
        return m.Groups[1].Value.Split(",").Select(s => s.Trim()).ToArray();
    }
}