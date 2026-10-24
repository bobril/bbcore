using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Lib.Registry;

public class PackageJson
{
    public static PackageJson Parse(JsonReader reader)
    {
        return JObject.Load(reader).ToObject<PackageJson>() ??
               throw new JsonSerializationException("Cannot parse package.json");
    }

    public required string Version { get; set; }
    public required string Name { get; set; }
    public required PackageJsonDist Dist { get; set; }
    public Dictionary<string, string>? Dependencies { get; set; }
    public Dictionary<string, string>? DevDependencies { get; set; }
}