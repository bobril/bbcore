using System.Text.Json;
using System.Text.Json.Serialization;

namespace Njsast.Utils;

public static class JsonHelpers
{
    public static readonly JsonSerializerOptions IgnoreNull = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
