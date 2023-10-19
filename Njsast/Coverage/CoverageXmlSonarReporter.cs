using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace Njsast.Coverage;

public class CoverageXmlSonarReporter: CoverageReporterBase
{
    readonly string? _commonSourceDirectory;
    readonly string _jsonName;
    XmlWriter? _xmlWriter;

    public CoverageXmlSonarReporter(CoverageInstrumentation covInstr, string? xmlName = null,
        string? commonSourceDirectory = null): base(covInstr)
    {
        _commonSourceDirectory = commonSourceDirectory;
        _jsonName = xmlName ?? "coverage-sonar.xml";
    }

    public override void Run()
    {
        using var stream = File.Create(_jsonName);
        using var xmlWriter = XmlWriter.Create(stream, new XmlWriterSettings {Indent = true});
        _xmlWriter = xmlWriter;
        base.Run();
    }

    public override void OnStartRoot(CoverageStats stats)
    {
        _xmlWriter!.WriteStartElement("coverage");
        _xmlWriter!.WriteAttributeString("version", "1");
    }

    public override void OnFinishRoot(CoverageStats stats)
    {
        _xmlWriter!.WriteEndElement();
    }
    
    public override void OnStartFile(CoverageFile file)
    {
        if (file.Stats!.LinesTotal==0) return;
        _xmlWriter!.WriteStartElement("file");
        _xmlWriter!.WriteAttributeString("path", Path.Combine(_commonSourceDirectory ?? "", file.RealName ?? file.FileName));
        var linesCovered = new HashSet<int>();
        var linesUncovered = new HashSet<int>();
        var branches = new RefDictionary<int, (int, int)>();
        foreach (var info in file.Infos)
        {
            switch (info.Source.Type)
            {
                case InstrumentedInfoType.Statement:
                case InstrumentedInfoType.Function:
                {
                    var hits = info.Hits;
                    var line = info.Source.Start.Line;
                    if (hits > 0)
                    {
                        linesCovered.Add(line);
                    }
                    else
                    {
                        linesUncovered.Add(line);
                    }
                    break;
                }
                case InstrumentedInfoType.Condition:
                {
                    var hitsFalsy = info.Hits;
                    var hitsTruthy = info.SecondaryHits;
                    var line = info.Source.Start.Line;
                    if (hitsFalsy > 0 && hitsTruthy > 0)
                    {
                        ref var b = ref branches.GetOrAddValueRef(line);
                        b = (b.Item1+2, b.Item2+2);
                        linesCovered.Add(line);
                    }
                    else if (hitsFalsy + hitsTruthy > 0)
                    {
                        ref var b = ref branches.GetOrAddValueRef(line);
                        b = (b.Item1+1, b.Item2+2);
                        linesCovered.Add(line);
                        linesUncovered.Add(line);
                    }
                    else
                    {
                        ref var b = ref branches.GetOrAddValueRef(line);
                        b = (b.Item1, b.Item2+2);
                        linesUncovered.Add(line);
                    }
                    break;
                }
                case InstrumentedInfoType.SwitchBranch:
                {
                    var hits = info.Hits;
                    var line = info.Source.Start.Line;
                    if (hits > 0)
                    {
                        ref var b = ref branches.GetOrAddValueRef(line);
                        b = (b.Item1+1, b.Item2+1);
                        linesCovered.Add(line);
                    }
                    else
                    {
                        ref var b = ref branches.GetOrAddValueRef(line);
                        b = (b.Item1, b.Item2+1);
                        linesUncovered.Add(line);
                    }
                    break;
                }
                default:
                    throw new InvalidDataException();
            }
        }

        var maxLine = linesCovered.Union(linesUncovered).Max();

        for (var line = 0; line <= maxLine; line++)
        {
            if (linesCovered.Contains(line) || linesUncovered.Contains(line))
            {
                _xmlWriter!.WriteStartElement("lineToCover");
                _xmlWriter!.WriteAttributeString("lineNumber", (line + 1).ToString());
                _xmlWriter!.WriteAttributeString("covered", linesCovered.Contains(line) ? "true" : "false");
                var b = branches.GetOrAddValueRef(line);
                if (b.Item2 > 0)
                {
                    _xmlWriter!.WriteAttributeString("branchesToCover", b.Item2.ToString());
                    _xmlWriter!.WriteAttributeString("coveredBranches", b.Item1.ToString());
                }
                _xmlWriter!.WriteEndElement();
            }
        }
        
        _xmlWriter!.WriteEndElement();
    }
}
