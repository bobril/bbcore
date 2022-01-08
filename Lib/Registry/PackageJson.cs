using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Lib.Registry;

public class PackageJson
{
    public static PackageJson Parse(JsonReader reader)
    {
        return JObject.Load(reader).ToObject<PackageJson>();
    }

    public string Version { get; set; }
    public string Name { get; set; }
    public PackageJsonDist Dist { get; set; }
    public Dictionary<string, string> Dependencies { get; set; }
    public Dictionary<string, string> DevDependencies { get; set; }
}