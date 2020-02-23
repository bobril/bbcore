namespace Njsast.Coverage
{
    public class CoverageInfo
    {
        public CoverageInfo(InstrumentedInfo source)
        {
            Source = source;
        }

        public readonly InstrumentedInfo Source;
        public uint Hits; // Falsy, Function, Statement, SwitchBranch Hits
        public uint SecondaryHits; // Truthy Hits
    }
}
