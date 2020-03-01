using System;

namespace Njsast.Coverage
{
    public class CoverageStats
    {
        public string Name;
        public string FullName;
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

        public StructList<CoverageStats> SubDirectories;
        public StructList<CoverageFile> SubFiles;

        public CoverageStats(string name, string fullName)
        {
            Name = name;
            FullName = fullName;
        }

        public string ConditionsPercentageText
        {
            get
            {
                if (ConditionsTotal == 0) return "N/A";
                return $"{(ConditionsCoveredFully * 2 + ConditionsCoveredPartially) * 50.0 / ConditionsTotal:F1}%";
            }
        }

        public string LinesPercentageText
        {
            get
            {
                if (LinesTotal == 0) return "N/A";
                return $"{(LinesCoveredFully * 2 + LinesCoveredPartially) * 50.0 / LinesTotal:F1}%";
            }
        }

        public string StatementsPercentageText
        {
            get
            {
                if (StatementsTotal == 0) return "N/A";
                return $"{StatementsCovered * 100.0 / StatementsTotal:F1}%";
            }
        }

        public string SwitchBranchesPercentageText
        {
            get
            {
                if (SwitchBranchesTotal == 0) return "N/A";
                return $"{SwitchBranchesCovered * 100.0 / SwitchBranchesTotal:F1}%";
            }
        }

        public string FunctionsPercentageText
        {
            get
            {
                if (FunctionsTotal == 0) return "N/A";
                return $"{FunctionsCovered * 100.0 / FunctionsTotal:F1}%";
            }
        }

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
