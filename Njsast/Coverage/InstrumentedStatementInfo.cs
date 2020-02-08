using System.Text.Json.Serialization;
using Njsast.Reader;

namespace Njsast.Coverage
{
    [JsonConverter(typeof(InstrumentedStatementInfoConverter))]
    public class InstrumentedStatementInfo
    {
        public string? FileName;
        public int Index;
        public Position Start;
        public Position End;
    }
}