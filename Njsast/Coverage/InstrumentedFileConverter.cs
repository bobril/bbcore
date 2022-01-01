using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Njsast.Coverage;

public class InstrumentedFileConverter : JsonConverter<InstrumentedFile>
{
    public override InstrumentedFile Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        throw new NotImplementedException();

    public override void Write(
        Utf8JsonWriter writer,
        InstrumentedFile value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("fileName", value.FileName);
        writer.WriteStartArray("infos");
        foreach (var info in value.Infos)
        {
            JsonSerializer.Serialize(writer, info, options);
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}