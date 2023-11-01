using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Lib.DiskCache;
using Lib.Utils;
using Shared.Utils;

namespace Lib.Registry;

public class DiffChangeLog
{
    readonly IDiskCache _diskCache;

    public DiffChangeLog(IDiskCache diskCache)
    {
        _diskCache = diskCache;
    }

    static SemanticVersioning.Version? TryParseVersion(string str)
    {
        try
        {
            return new(str);
        }
        catch
        {
            return null;
        }
    }

    static Regex _lineWithVersion = new Regex("^#+\\s*(\\d+\\.\\d+\\.\\d+)(?>\\s+.*)?$", RegexOptions.Compiled);

    public IEnumerable<string> Generate(PackagePathVersion[] before, PackagePathVersion[] after)
    {
        var beforeDict = before.ToDictionary(version => version.Name);
        foreach (var packagePathVersion in after)
        {
            if (beforeDict.TryGetValue(packagePathVersion.Name, out var beforePackage))
            {
                if (beforePackage.Version == packagePathVersion.Version)
                    continue;
                var verFrom = TryParseVersion(beforePackage.Version);
                var verTo = TryParseVersion(packagePathVersion.Version);
                if (verFrom == null || verTo == null) continue;
                var file =
                    _diskCache.TryGetItem(PathUtils.Join(packagePathVersion.Path, "CHANGELOG.md")) as IFileCache;
                if (file == null)
                    continue;
                var diffLines = file.Utf8Content.Split('\n').Select(l => l.TrimEnd('\r')).Aggregate(
                    (new List<string>(), false),
                    (acc, line) =>
                    {
                        var read = acc.Item2;
                        var verMatch = _lineWithVersion.Match(line);
                        if (verMatch.Success)
                        {
                            var verStr = verMatch.Groups[1].Value;
                            var ver = TryParseVersion(verStr);
                            if (ver != null)
                                read = verFrom < ver && ver <= verTo;
                        }
                        if (read)
                            acc.Item1.Add(line);
                        return (acc.Item1, read);
                    }, acc => acc.Item1);
                if (diffLines.Count == 0)
                    continue;
                yield return $"# {packagePathVersion.Name}";
                yield return $"# {beforePackage.Version} => {packagePathVersion.Version}";
                foreach (var line in diffLines)
                {
                    yield return line;
                }

                yield return "";
            }
        }
    }
}