using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Lib.Utils;

public static class JsonHelpers
{
    public static readonly JsonSerializerOptions IgnoreNull = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static readonly JsonSerializerOptions IndentedIgnoreNull = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        IncludeFields = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static JsonNode? GetValue(this JsonObject obj, string name)
    {
        return obj.TryGetPropertyValue(name, out var value) ? value : null;
    }

    public static T? Value<T>(this JsonNode? node)
    {
        return node is JsonValue value ? value.GetValue<T>() : node == null ? default : node.Deserialize<T>();
    }

    public static T? Value<T>(this JsonNode? node, string name)
    {
        return node is JsonObject obj && obj.TryGetPropertyValue(name, out var value) ? value.Value<T>() : default;
    }

    public static IEnumerable<T?> Values<T>(this JsonNode? node)
    {
        return node is JsonArray array ? array.Select(i => i.Value<T>()) : Enumerable.Empty<T?>();
    }

    public static IEnumerable<JsonPropertyCompat> Properties(this JsonObject obj)
    {
        return obj.Select(i => new JsonPropertyCompat(i.Key, i.Value));
    }

    public static JsonObject ParseObject(string json)
    {
        try
        {
            return JsonNode.Parse(json, null,
                new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip })!.AsObject();
        }
        catch (JsonException)
        {
            return JsonNode.Parse(ToStrictJson(json), null,
                new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip })!.AsObject();
        }
    }

    static string ToStrictJson(string json)
    {
        var sb = new System.Text.StringBuilder(json.Length + 16);
        var expectProperty = false;
        for (var i = 0; i < json.Length; i++)
        {
            var c = json[i];
            if (c == '\'' || c == '"')
            {
                var quote = c;
                sb.Append('"');
                i++;
                while (i < json.Length)
                {
                    c = json[i];
                    if (c == '\\' && i + 1 < json.Length)
                    {
                        sb.Append('\\');
                        sb.Append(json[++i]);
                    }
                    else if (c == quote)
                    {
                        break;
                    }
                    else
                    {
                        if (c == '"') sb.Append('\\');
                        sb.Append(c);
                    }

                    i++;
                }

                sb.Append('"');
                expectProperty = false;
                continue;
            }

            if (c is '{' or ',')
            {
                expectProperty = true;
                sb.Append(c);
                continue;
            }

            if (expectProperty && char.IsWhiteSpace(c))
            {
                sb.Append(c);
                continue;
            }

            if (expectProperty && (char.IsLetter(c) || c == '_' || c == '$' || c == '#'))
            {
                var start = i;
                while (i < json.Length && (char.IsLetterOrDigit(json[i]) || json[i] is '_' or '$' or '-' or '#' or '*'))
                    i++;
                sb.Append('"');
                sb.Append(json, start, i - start);
                sb.Append('"');
                i--;
                expectProperty = false;
                continue;
            }

            if (c == ':') expectProperty = false;
            sb.Append(c);
        }

        return sb.ToString();
    }
}

public readonly record struct JsonPropertyCompat(string Name, JsonNode? Value);
