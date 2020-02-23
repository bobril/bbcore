using System.Text.Json.Serialization;
using Njsast.Utils;

namespace Njsast.Coverage
{
    [JsonConverter(typeof(InstrumentedInfoConverter))]
    public class InstrumentedInfo
    {
        public InstrumentedInfo(InstrumentedInfoType type, int index, LineCol start, LineCol end)
        {
            Type = type;
            Index = index;
            Start = start;
            End = end;
        }

        public InstrumentedInfoType Type;
        public int Index;
        public LineCol Start;
        public LineCol End;
    }
}
