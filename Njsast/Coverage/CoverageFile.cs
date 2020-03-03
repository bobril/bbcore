using System;
using System.Collections.Generic;
using System.Linq;
using Njsast.Utils;

namespace Njsast.Coverage
{
    public class CoverageFile
    {
        public string Name;
        public string FileName;
        public string? RealName;
        public bool Important;
        public CoverageStats? Stats;
        public StructList<CoverageInfo> Infos;

        public CoverageFile(string fileName, string? realName, InstrumentedFile file)
        {
            FileName = fileName;
            RealName = realName;
            Name = PathUtils.Name(fileName);
            Important = file.Important;
            Infos = new StructList<CoverageInfo>();
            Infos.Reserve(file.Infos.Count);
            foreach (var info in file.Infos)
            {
                Infos.Add(new CoverageInfo(info));
            }
        }

        public void AddHits(ReadOnlySpan<uint> hits)
        {
            foreach (var info in Infos)
            {
                var idx = info.Source.Index;
                info.Hits += hits[idx];
                if (info.Source.Type == InstrumentedInfoType.Condition)
                {
                    info.SecondaryHits += hits[idx + 1];
                }
            }
        }

        public void CalcStats()
        {
            Stats = new CoverageStats(Name, FileName);
            var stats = Stats;
            var linesCovered = new HashSet<int>();
            var linesUncovered = new HashSet<int>();
            foreach (var info in Infos)
            {
                switch (info.Source.Type)
                {
                    case InstrumentedInfoType.Statement:
                    {
                        var hits = info.Hits;
                        var line = info.Source.Start.Line;
                        if (hits > 0)
                        {
                            if (stats.StatementsMaxHits < hits)
                                stats.StatementsMaxHits = hits;
                            stats.StatementsCovered++;
                            linesCovered.Add(line);
                        }
                        else
                        {
                            linesUncovered.Add(line);
                        }

                        stats.StatementsTotal++;

                        break;
                    }
                    case InstrumentedInfoType.Condition:
                    {
                        var hitsFalsy = info.Hits;
                        var hitsTruthy = info.SecondaryHits;
                        var line = info.Source.Start.Line;
                        if (stats.ConditionsMaxHits < hitsFalsy + hitsTruthy)
                            stats.ConditionsMaxHits = hitsFalsy + hitsTruthy;
                        if (hitsFalsy > 0 && hitsTruthy > 0)
                        {
                            stats.ConditionsCoveredFully++;
                            linesCovered.Add(line);
                        }
                        else if (hitsFalsy + hitsTruthy > 0)
                        {
                            stats.ConditionsCoveredPartially++;
                            linesCovered.Add(line);
                            linesUncovered.Add(line);
                        }
                        else
                        {
                            linesUncovered.Add(line);
                        }

                        stats.ConditionsTotal++;

                        break;
                    }
                    case InstrumentedInfoType.Function:
                    {
                        var hits = info.Hits;
                        var line = info.Source.Start.Line;
                        if (hits > 0)
                        {
                            if (stats.FunctionsMaxHits < hits)
                                stats.FunctionsMaxHits = hits;
                            stats.FunctionsCovered++;
                            linesCovered.Add(line);
                        }
                        else
                        {
                            linesUncovered.Add(line);
                        }

                        stats.FunctionsTotal++;

                        break;
                    }
                    case InstrumentedInfoType.SwitchBranch:
                    {
                        var hits = info.Hits;
                        var line = info.Source.Start.Line;
                        if (hits > 0)
                        {
                            if (stats.SwitchBranchesMaxHits < hits)
                                stats.SwitchBranchesMaxHits = hits;
                            stats.SwitchBranchesCovered++;
                            linesCovered.Add(line);
                        }
                        else
                        {
                            linesUncovered.Add(line);
                        }

                        stats.SwitchBranchesTotal++;

                        break;
                    }
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            stats.LinesMaxHits =
                Math.Max(Math.Max(Math.Max(stats.StatementsMaxHits, stats.FunctionsMaxHits), stats.ConditionsMaxHits),
                    stats.SwitchBranchesMaxHits);
            stats.LinesTotal = (uint) linesCovered.Union(linesUncovered).Count();
            stats.LinesCoveredPartially = (uint) linesCovered.Intersect(linesUncovered).Count();
            stats.LinesCoveredFully = (uint) linesCovered.Count - stats.LinesCoveredPartially;
        }
    }
}
