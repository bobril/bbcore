using System;
using System.Collections.Generic;
using Njsast.Ast;
using Njsast.Reader;
using Njsast.Utils;

namespace Njsast.Coverage
{
    public class CoverageInstrumentation
    {
        public Dictionary<string, InstrumentedFile> InstrumentedFiles;
        public Dictionary<string, CoverageFile> CoveredFiles;
        public Dictionary<string, CoverageStats> DirectoryStats;
        public int LastIndex;
        public readonly string StorageName;
        public readonly string FncNameCond;
        public readonly string FncNameStatement;
        public Func<string, string?>? RealPath;
        public ITextFileReader? SourceReader;

        public CoverageInstrumentation(string storageName = "__c0v")
        {
            InstrumentedFiles = new Dictionary<string, InstrumentedFile>();
            CoveredFiles = new Dictionary<string, CoverageFile>();
            DirectoryStats = new Dictionary<string, CoverageStats>();
            StorageName = storageName;
            FncNameCond = storageName + "C";
            FncNameStatement = storageName + "S";
        }

        public AstToplevel Instrument(AstToplevel toplevel)
        {
            var tt = new InstrumentTreeTransformer(this);
            toplevel = (AstToplevel) tt.Transform(toplevel);
            return toplevel;
        }

        public void AddCountingHelpers(AstToplevel toplevel, string globalThis = "window")
        {
            var tla = Parser.Parse(
                $"var {StorageName}=new Uint32Array({LastIndex});{globalThis}.{StorageName}={StorageName};function {FncNameStatement}(i){{{StorageName}[i]++;}}function {FncNameCond}(r,i){{{StorageName}[i+(r?1:0)]++;return r}}");
            toplevel.Body.InsertRange(0, tla.Body);
        }

        internal InstrumentedFile GetForFile(string name)
        {
            if (!InstrumentedFiles.TryGetValue(name, out var res))
            {
                string? real = null;
                if (RealPath != null)
                {
                    real = RealPath.Invoke(name);
                    if (real == name)
                    {
                        real = null;
                    }
                }

                res = new InstrumentedFile(name, real);
                InstrumentedFiles.Add(name, res);
                if (real != null)
                {
                    InstrumentedFiles[real] = res;
                }
            }

            return res;
        }

        public void CleanUp(ITextFileReader? reader)
        {
            SourceReader = reader;
            foreach (var (name, fileInfo) in InstrumentedFiles)
            {
                fileInfo.Sort();
                if (reader != null)
                {
                    var content = reader.ReadUtf8(name);
                    if (!content.IsEmpty)
                    {
                        fileInfo.PruneWhiteSpace(content);
                    }
                }
            }
        }

        public void BuildCoveredFiles(string? commonRoot = null)
        {
            CoveredFiles.Clear();
            foreach (var keyValuePair in InstrumentedFiles)
            {
                var fn = keyValuePair.Key;
                var fileName = keyValuePair.Value.FileName;
                var realName = keyValuePair.Value.RealName;
                if (commonRoot != null)
                {
                    fn = PathUtils.Subtract(fn, commonRoot);
                    fileName = PathUtils.Subtract(fileName, commonRoot);
                    if (realName != null)
                        realName = PathUtils.Subtract(realName, commonRoot);
                }

                if (realName == null || fn == realName)
                {
                    var cf = new CoverageFile(fileName, realName, keyValuePair.Value);
                    CoveredFiles[fn] = cf;
                    if (realName != null)
                    {
                        CoveredFiles[fileName] = cf;
                    }
                }
            }
        }

        public void AddHits(ReadOnlySpan<uint> hits)
        {
            if (CoveredFiles.Count == 0)
                BuildCoveredFiles();
            foreach (var keyValuePair in CoveredFiles)
            {
                keyValuePair.Value.AddHits(hits);
            }
        }

        public void CalcStats(bool justImportant = false)
        {
            DirectoryStats[""] = new CoverageStats("Total", "");
            foreach (var keyValuePair in CoveredFiles)
            {
                keyValuePair.Value.CalcStats();
                if (justImportant && !keyValuePair.Value.Important) continue;
                var d = keyValuePair.Key;
                object? link = keyValuePair.Value;
                do
                {
                    d = PathUtils.Parent(d);
                    object? nextLink;
                    if (!DirectoryStats.TryGetValue(d, out var stats))
                    {
                        stats = new CoverageStats(PathUtils.Name(d), d);
                        DirectoryStats[d] = stats;
                        nextLink = stats;
                    }
                    else
                    {
                        nextLink = null;
                    }

                    if (link != null)
                    {
                        if (link is CoverageStats)
                        {
                            stats.SubDirectories.Add((CoverageStats) link);
                        }
                        else
                        {
                            stats.SubFiles.Add((CoverageFile) link);
                        }
                    }

                    stats.Add(keyValuePair.Value.Stats!);

                    link = nextLink;
                } while (d != "");
            }
        }
    }
}
