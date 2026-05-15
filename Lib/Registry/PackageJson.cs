using System.Collections.Generic;
using System.Text.Json;

namespace Lib.Registry;

public class PackageJson
{
    public static PackageJson Parse(JsonElement element)
    {
        var result = new PackageJson();
        if (element.TryGetProperty("version", out var version)) result.Version = version.GetString();
        if (element.TryGetProperty("name", out var name)) result.Name = name.GetString();
        if (element.TryGetProperty("dist", out var dist))
        {
            result.Dist = new();
            if (dist.TryGetProperty("tarball", out var tarball)) result.Dist.Tarball = tarball.GetString();
        }

        return result;
    }

    public string Version { get; set; }
    public string Name { get; set; }
    public PackageJsonDist Dist { get; set; }
    public Dictionary<string, string> Dependencies { get; set; }
    public Dictionary<string, string> DevDependencies { get; set; }
}
