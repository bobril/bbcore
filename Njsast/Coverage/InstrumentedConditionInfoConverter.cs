using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Njsast.Coverage
{
    public class InstrumentedConditionInfoConverter : JsonConverter<InstrumentedConditionInfo>
    {
        public override InstrumentedConditionInfo Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options) =>
            throw new NotImplementedException();

        public override void Write(
            Utf8JsonWriter writer,
            InstrumentedConditionInfo value,
            JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("index", value.Index);
            if (value.FileName != null) writer.WriteString("fileName", value.FileName);
            writer.WriteString("start", value.Start.ToShortString());
            writer.WriteString("end", value.End.ToShortString());
            writer.WriteEndObject();
        }
    }
}