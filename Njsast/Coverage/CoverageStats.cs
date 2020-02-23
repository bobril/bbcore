using System;

namespace Njsast.Coverage
{
    public class CoverageStats
    {
        public uint StatementsCovered;
        public uint StatementsTotal;
        public uint StatementsMaxHits;
        public uint ConditionsCoveredPartially;
        public uint ConditionsCoveredFully;
        public uint ConditionsTotal;
        public uint ConditionsMaxHits;
        public uint FunctionsCovered;
        public uint FunctionsTotal;
        public uint FunctionsMaxHits;
        public uint SwitchBranchesCovered;
        public uint SwitchBranchesTotal;
        public uint SwitchBranchesMaxHits;
        public uint LinesCoveredPartially;
        public uint LinesCoveredFully;
        public uint LinesTotal;
        public uint LinesMaxHits;

        public void Add(CoverageStats s)
        {
            StatementsCovered += s.StatementsCovered;
            StatementsTotal += s.StatementsTotal;
            StatementsMaxHits = Math.Max(StatementsMaxHits, s.StatementsMaxHits);
            FunctionsCovered += s.FunctionsCovered;
            FunctionsTotal += s.FunctionsTotal;
            FunctionsMaxHits = Math.Max(FunctionsMaxHits, s.FunctionsMaxHits);
            ConditionsCoveredFully += s.ConditionsCoveredFully;
            ConditionsCoveredPartially += s.ConditionsCoveredPartially;
            ConditionsTotal += s.ConditionsTotal;
            ConditionsMaxHits = Math.Max(ConditionsMaxHits, s.ConditionsMaxHits);
            SwitchBranchesCovered += s.SwitchBranchesCovered;
            SwitchBranchesTotal += s.SwitchBranchesTotal;
            SwitchBranchesMaxHits = Math.Max(SwitchBranchesMaxHits, s.SwitchBranchesMaxHits);
            LinesCoveredFully += s.LinesCoveredFully;
            LinesCoveredPartially += s.LinesCoveredPartially;
            LinesTotal += s.LinesTotal;
            LinesMaxHits = Math.Max(LinesMaxHits, s.LinesMaxHits);
        }
    }
}
