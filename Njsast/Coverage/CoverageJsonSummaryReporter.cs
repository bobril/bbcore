using System;
using System.Text.Json;

namespace Njsast.Coverage
{
    public class CoverageJsonSummaryReporter: CoverageReporterBase
    {
        Utf8JsonWriter? _jsonWriter;

        public CoverageJsonSummaryReporter(CoverageInstrumentation covInstr): base(covInstr)
        {
        }

        public override void Run()
        {
            using var stream = System.IO.File.Create("coverage-summary.json");
            using var jsonWriter = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
            _jsonWriter = jsonWriter;
            base.Run();
        }

        public override void OnStartRoot(CoverageStats stats)
        {
            _jsonWriter!.WriteStartObject();
            _jsonWriter!.WritePropertyName("Total");
            WriteStats(stats);
        }

        void WriteStats(CoverageStats stats)
        {
            _jsonWriter!.WriteStartObject();
            _jsonWriter!.WriteString("statements",stats.StatementsPercentageText);
            _jsonWriter!.WriteNumber("statementsCovered",stats.StatementsCovered);
            _jsonWriter!.WriteNumber("statementsTotal",stats.StatementsTotal);
            _jsonWriter!.WriteNumber("statementsMaxHits",stats.StatementsMaxHits);
            _jsonWriter!.WriteString("conditions",stats.ConditionsPercentageText);
            _jsonWriter!.WriteNumber("conditionsCoveredPartially", stats.ConditionsCoveredPartially);
            _jsonWriter!.WriteNumber("conditionsCoveredFully", stats.ConditionsCoveredFully);
            _jsonWriter!.WriteNumber("conditionsTotal",stats.ConditionsTotal);
            _jsonWriter!.WriteNumber("conditionsMaxHits",stats.ConditionsMaxHits);
            _jsonWriter!.WriteString("functions", stats.FunctionsPercentageText);
            _jsonWriter!.WriteNumber("functionsCovered",stats.FunctionsCovered);
            _jsonWriter!.WriteNumber("functionsTotal",stats.FunctionsTotal);
            _jsonWriter!.WriteNumber("functionsMaxHits",stats.FunctionsMaxHits);
            _jsonWriter!.WriteString("switchBranches", stats.SwitchBranchesPercentageText);
            _jsonWriter!.WriteNumber("switchBranchesCovered", stats.SwitchBranchesCovered);
            _jsonWriter!.WriteNumber("switchBranchesTotal", stats.SwitchBranchesTotal);
            _jsonWriter!.WriteNumber("switchBranchesMaxHits", stats.SwitchBranchesMaxHits);
            _jsonWriter!.WriteString("lines",stats.LinesPercentageText);
            _jsonWriter!.WriteNumber("linesCoveredPartially", stats.LinesCoveredPartially);
            _jsonWriter!.WriteNumber("linesCoveredFully", stats.LinesCoveredFully);
            _jsonWriter!.WriteNumber("linesTotal", stats.LinesTotal);
            _jsonWriter!.WriteNumber("linesMaxHits", stats.LinesMaxHits);
            _jsonWriter!.WriteEndObject();
        }

        public override void OnFinishRoot(CoverageStats stats)
        {
            _jsonWriter!.WriteEndObject();
        }

        public override void OnStartDirectory(CoverageStats stats)
        {
            _jsonWriter!.WritePropertyName(stats.FullName);
            WriteStats(stats);
        }

        public override void OnStartFile(CoverageFile file)
        {
            _jsonWriter!.WritePropertyName(file.RealName ?? file.FileName);
            WriteStats(file.Stats!);
        }
    }
}
