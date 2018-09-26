using System;
using System.Collections.Generic;
using System.Linq;
using Lib.DiskCache;
using Lib.Utils;

namespace Lib.Registry
{
    public class DiffChangeLog
    {
        readonly IDiskCache _diskCache;

        public DiffChangeLog(IDiskCache diskCache)
        {
            _diskCache = diskCache;
        }

        public IEnumerable<string> Generate(PackagePathVersion[] before, PackagePathVersion[] after)
        {
            var beforeDict = before.ToDictionary(version => version.Name);
            foreach (var packagePathVersion in after)
            {
                if (beforeDict.TryGetValue(packagePathVersion.Name, out var beforePackage))
                {
                    if (beforePackage.Version == packagePathVersion.Version)
                        continue;
                    var file =
                        _diskCache.TryGetItem(PathUtils.Join(packagePathVersion.Path, "CHANGELOG.md")) as IFileCache;
                    if (file == null)
                        continue;
                    var diffLines = file.Utf8Content.Split('\n').Select(l => l.TrimEnd('\r')).Aggregate(
                        (new List<string>(), false),
                        (acc, line) =>
                        {
                            var read = acc.Item2;
                            var possiblyVersion = line.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                .ElementAtOrDefault(1);
                            if (possiblyVersion == packagePathVersion.Version)
                                read = true;
                            if (possiblyVersion == beforePackage.Version)
                                read = false;
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
}
