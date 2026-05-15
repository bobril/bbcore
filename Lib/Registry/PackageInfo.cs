using System;
using System.Collections.Generic;
using System.Text.Json;
using Version = SemanticVersioning.Version;

namespace Lib.Registry;

public class PackageInfo
{
    readonly string _content;
    Dictionary<string, Version> _distTags;

    public PackageInfo(string content)
    {
        _content = content;
    }

    enum State
    {
        Start,
        Main,
        Versions
    }

    public Dictionary<string, Version> DistTags()
    {
        if (_distTags == null)
        {
            _distTags = new Dictionary<string, Version>();
            foreach (var tuple in ParseDistTags())
            {
                _distTags[tuple.Item1] = tuple.Item2;
            }
        }

        return _distTags;
    }

    public IEnumerable<(string, Version)> ParseDistTags()
    {
        using var document = JsonDocument.Parse(_content);
        if (!document.RootElement.TryGetProperty("dist-tags", out var distTags))
            yield break;
        foreach (var tag in distTags.EnumerateObject())
        {
            yield return (tag.Name, new Version(tag.Value.GetString(), true));
        }
    }

    public void LazyParseVersions(Func<Version, bool> shouldReadVersion, Action<JsonElement> versionContent)
    {
        using var document = JsonDocument.Parse(_content);
        if (!document.RootElement.TryGetProperty("versions", out var versions))
            return;
        foreach (var property in versions.EnumerateObject())
        {
            var ver = new Version(property.Name, true);
            if (shouldReadVersion(ver))
            {
                versionContent(property.Value);
            }
        }
    }
}