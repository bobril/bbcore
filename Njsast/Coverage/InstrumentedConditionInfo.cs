using System.Text.Json.Serialization;
using Njsast.Reader;

namespace Njsast.Coverage
{
    [JsonConverter(typeof(InstrumentedConditionInfoConverter))]
    public class InstrumentedConditionInfo
    {
        public string? FileName;
        public int Index; // False = Index, True = Index + 1
        public Position Start;
        public Position End;
    }
}