using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Njsast.Coverage
{
    public class InstrumentedInfoConverter : JsonConverter<InstrumentedInfo>
    {
        public override InstrumentedInfo Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options) =>
            throw new NotImplementedException();

        public override void Write(
            Utf8JsonWriter writer,
            InstrumentedInfo value,
            JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString("type", value.Type.ToString());
            writer.WriteNumber("index", value.Index);
            writer.WriteString("start", value.Start.ToString());
            writer.WriteString("end", value.End.ToString());
            writer.WriteEndObject();
        }
    }
}
