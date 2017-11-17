using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lib.Utils
{
    public class SourceMap
    {
        public SourceMap()
        {
            version = 3;
        }

        public int version { get; set; }
        public string file { get; set; }
        public string sourceRoot { get; set; }
        public List<string> sources { get; set; }
        public List<string> sourcesContent { get; set; }
        public List<string> names { get; set; }
        public string mappings { get; set; }

        public override string ToString()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(this);
        }

        public static SourceMap Empty()
        {
            return new SourceMap
            {
                sources = new List<string>(),
                mappings = ""
            };
        }

        public static SourceMap Identity(string content, string fileName)
        {
            var sb = new StringBuilder();
            sb.Append("AAAA");
            for (var i = 0; i < content.Length; i++)
                if (content[i] == '\n') sb.Append(";AACA");
            return new SourceMap
            {
                sources = new List<string> { fileName },
                mappings = sb.ToString()
            };
        }

        public static SourceMap Parse(string content, string dir)
        {
            var res = Newtonsoft.Json.JsonConvert.DeserializeObject<SourceMap>(content);
            if (res.version != 3) throw new Exception("Invalid Source Map version " + res.version);
            if (dir != null)
            {
                res.sources = res.sources.Select(s => PathUtils.Join(dir, s)).ToList();
            }
            return res;
        }

        public static string RemoveLinkToSourceMap(string content)
        {
            var pos = content.Length - 3;
            while (pos >= 0)
            {
                if (content[pos] == 10) break;
                pos--;
            }
            if (pos < content.Length - 5)
            {
                if (content.Substring(pos + 1, 3) == "//#")
                    return content.Substring(0, pos);
            }
            return content;
        }

        public SourceCodePosition FindPosition(int line, int col)
        {
            var inputMappings = this.mappings;
            var outputLine = 1;
            var ip = 0;
            var inOutputCol = 0;
            var inSourceIndex = 0;
            var inSourceLine = 0;
            var inSourceCol = 0;
            var shift = 0;
            var value = 0;
            var valpos = 0;
            var lastOutputCol = 0;
            var lastSourceIndex = 0;
            var lastSourceLine = 0;
            var lastSourceCol = 0;
            var res = new SourceCodePosition();
            void commit()
            {
                if (valpos == 0) return;
                if (outputLine == line && lastOutputCol <= col && col <= inOutputCol)
                {
                    if (lastSourceIndex < 0) return;
                    res.SourceName = sources[inSourceIndex];
                    res.Line = lastSourceLine + 1;
                    res.Col = lastSourceCol + col - lastOutputCol;
                    return;
                }
                if (valpos == 1)
                {
                    lastSourceIndex = -1;
                }
                else
                {
                    lastSourceIndex = inSourceIndex;
                    lastSourceLine = inSourceLine;
                    lastSourceCol = inSourceCol;
                    if (outputLine == line && col == 0)
                    {
                        res.SourceName = sources[inSourceIndex];
                        res.Line = inSourceLine + 1;
                        res.Col = inSourceCol;
                        return;
                    }
                }
                lastOutputCol = inOutputCol;
                valpos = 0;
            }
            while (ip < inputMappings.Length)
            {
                var ch = inputMappings[ip++];
                if (ch == ';')
                {
                    commit();
                    inOutputCol = 0;
                    outputLine++;
                }
                else if (ch == ',')
                {
                    commit();
                }
                else
                {
                    var b = (int)SourceMapBuilder.char2int[ch];
                    if (b == 255) throw new Exception("Invalid sourceMap");
                    value += (b & 31) << shift;
                    if ((b & 32) != 0)
                    {
                        shift += 5;
                    }
                    else
                    {
                        var shouldNegate = value & 1;
                        value >>= 1;
                        if (shouldNegate != 0) value = -value;
                        switch (valpos)
                        {
                            case 0: inOutputCol += value; break;
                            case 1: inSourceIndex += value; break;
                            case 2: inSourceLine += value; break;
                            case 3: inSourceCol += value; break;
                        }
                        valpos++;
                        value = shift = 0;
                    }
                }
            }
            commit();
            return res;
        }

    }
}

