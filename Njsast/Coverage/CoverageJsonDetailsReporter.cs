using System;
using System.Text;
using System.Text.Json;

namespace Njsast.Coverage;

public class CoverageJsonDetailsReporter: CoverageReporterBase
{
    readonly string _jsonName;
    readonly bool _script;
    Utf8JsonWriter? _jsonWriter;

    public CoverageJsonDetailsReporter(CoverageInstrumentation covInstr, string? jsonName = null, bool script = false): base(covInstr)
    {
        _jsonName = jsonName ?? "coverage-details.json";
        _script = script;
    }

    public override void Run()
    {
        using var stream = System.IO.File.Create(_jsonName);
        if (_script)
            stream.Write(Encoding.UTF8.GetBytes("var bbcoverage="));
        using var jsonWriter = new Utf8JsonWriter(stream);
        _jsonWriter = jsonWriter;
        base.Run();
    }

    public override void OnStartRoot(CoverageStats stats)
    {
        _jsonWriter!.WriteStartObject();
        _jsonWriter!.WritePropertyName("*");
        WriteStats(stats);
    }

    void WriteStats(CoverageStats stats, bool withEnd = true)
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
        if (stats.SubDirectories.Count > 0)
        {
            _jsonWriter!.WriteStartArray("subDirectories");
            foreach (var statsSubDirectory in stats.SubDirectories)
            {
                _jsonWriter!.WriteStringValue(statsSubDirectory.FullName);
            }
            _jsonWriter!.WriteEndArray();
        }

        if (stats.SubFiles.Count > 0)
        {
            _jsonWriter!.WriteStartArray("subFiles");
            foreach (var statsSubFile in stats.SubFiles)
            {
                _jsonWriter!.WriteStringValue(statsSubFile.RealName ?? statsSubFile.FileName);
            }
            _jsonWriter!.WriteEndArray();
        }
        if (withEnd)
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
        WriteStats(file.Stats!, false);
        _jsonWriter!.WriteStartArray("encodedRanges");
        foreach (var coverageInfo in file.Infos)
        {
            var info = coverageInfo.Source;
            switch (info.Type)
            {
                case InstrumentedInfoType.Statement:
                    _jsonWriter!.WriteNumberValue(0);
                    _jsonWriter!.WriteNumberValue(info.Start.Line);
                    _jsonWriter!.WriteNumberValue(info.Start.Col);
                    _jsonWriter!.WriteNumberValue(info.End.Line);
                    _jsonWriter!.WriteNumberValue(info.End.Col);
                    _jsonWriter!.WriteNumberValue((int) coverageInfo.Hits);
                    break;
                case InstrumentedInfoType.Condition:
                    _jsonWriter!.WriteNumberValue(1);
                    _jsonWriter!.WriteNumberValue(info.Start.Line);
                    _jsonWriter!.WriteNumberValue(info.Start.Col);
                    _jsonWriter!.WriteNumberValue(info.End.Line);
                    _jsonWriter!.WriteNumberValue(info.End.Col);
                    _jsonWriter!.WriteNumberValue((int) coverageInfo.Hits);
                    _jsonWriter!.WriteNumberValue((int) coverageInfo.SecondaryHits);
                    break;
                case InstrumentedInfoType.Function:
                    _jsonWriter!.WriteNumberValue(2);
                    _jsonWriter!.WriteNumberValue(info.Start.Line);
                    _jsonWriter!.WriteNumberValue(info.Start.Col);
                    _jsonWriter!.WriteNumberValue(info.End.Line);
                    _jsonWriter!.WriteNumberValue(info.End.Col);
                    _jsonWriter!.WriteNumberValue((int) coverageInfo.Hits);
                    break;
                case InstrumentedInfoType.SwitchBranch:
                    _jsonWriter!.WriteNumberValue(3);
                    _jsonWriter!.WriteNumberValue(info.Start.Line);
                    _jsonWriter!.WriteNumberValue(info.Start.Col);
                    _jsonWriter!.WriteNumberValue(info.End.Line);
                    _jsonWriter!.WriteNumberValue(info.End.Col);
                    _jsonWriter!.WriteNumberValue((int) coverageInfo.Hits);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        _jsonWriter!.WriteEndArray();
        if (_covInstr.SourceReader != null)
        {
            _jsonWriter!.WriteString("source", Encoding.UTF8.GetString(_covInstr.SourceReader.ReadUtf8(file.InstrumentedFile.FileName)));
        }
        _jsonWriter!.WriteEndObject();
    }
}