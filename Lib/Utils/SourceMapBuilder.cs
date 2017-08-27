using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lib.Utils
{
    public class SourceMapBuilder
    {
        StringBuilder _content = new StringBuilder();
        List<string> _sources = new List<string>();
        StringBuilder _mappings = new StringBuilder();
        int _lastSourceIndex = 0;
        int _lastSourceLine = 0;
        int _lastSourceCol = 0;

        public const string int2Char = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/=";
        // Just ASCII so length is 128
        // var b = new StringBuilder();
        // b.Append('\xff', 128);
        // for (var i = 0; i < a.Length; i++) b[int2Char[i]] = (char) i;
        // b.toString()
        public const string char2int = "ÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿ>ÿÿÿ?456789:;<=ÿÿÿ@ÿÿÿ\0\u0001\u0002\u0003\u0004\u0005\u0006\a\b\t\n\v\f\r\u000e\u000f\u0010\u0011\u0012\u0013\u0014\u0015\u0016\u0017\u0018\u0019ÿÿÿÿÿÿ\u001a\u001b\u001c\u001d\u001e\u001f !\"#$%&'()*+,-./0123ÿÿÿÿÿ";

        public void Clear()
        {
            _content.Clear();
            _mappings.Clear();
            _sources.Clear();
            _lastSourceIndex = 0;
            _lastSourceLine = 0;
            _lastSourceCol = 0;
        }

        public string Content()
        {
            return _content.ToString();
        }

        public SourceMap Build()
        {
            return new SourceMap
            {
                sources = _sources.ToList(),
                mappings = _mappings.ToString()
            };
        }

        public void AddText(string content)
        {
            _content.Append(content);
            var lines = content.Count(ch => ch == '\n');
            if (!content.EndsWith('\n'))
            {
                lines++;
                _content.Append('\n');
            }
            _mappings.Append(';', lines);
        }

        static void addVLQ(StringBuilder that, int num)
        {
            if (num < 0)
            {
                num = (-num << 1) | 1;
            }
            else
            {
                num <<= 1;
            }
            do
            {
                var clamped = num & 31;
                num >>= 5;
                if (num > 0)
                {
                    clamped |= 32;
                }
                that.Append(int2Char[clamped]);
            } while (num > 0);
        }

        public void AddSource(string content, SourceMap sourceMap = null)
        {
            if (sourceMap == null) sourceMap = SourceMap.Empty();
            _content.Append(content);
            var sourceLines = content.Count(ch => ch == '\n');
            if (!content.EndsWith('\n'))
            {
                sourceLines++;
                _content.Append('\n');
            }
            var sourceRemap = new List<int>();
            sourceMap.sources.ForEach((v) =>
            {
                var pos = _sources.IndexOf(v);
                if (pos < 0)
                {
                    pos = _sources.Count;
                    _sources.Add(v);
                }
                sourceRemap.Add(pos);
            });
            var lastOutputCol = 0;
            var inputMappings = sourceMap.mappings;
            var outputLine = 0;
            var ip = 0;
            var inOutputCol = 0;
            var inSourceIndex = 0;
            var inSourceLine = 0;
            var inSourceCol = 0;
            var shift = 0;
            var value = 0;
            var valpos = 0;
            void commit()
            {
                if (valpos == 0) return;
                addVLQ(_mappings, inOutputCol - lastOutputCol);
                lastOutputCol = inOutputCol;
                if (valpos == 1)
                {
                    valpos = 0;
                    return;
                }
                var outSourceIndex = sourceRemap[inSourceIndex];
                addVLQ(_mappings, outSourceIndex - _lastSourceIndex);
                _lastSourceIndex = outSourceIndex;
                addVLQ(_mappings, inSourceLine - _lastSourceLine);
                _lastSourceLine = inSourceLine;
                addVLQ(_mappings, inSourceCol - _lastSourceCol);
                _lastSourceCol = inSourceCol;
                valpos = 0;
            }
            while ((uint)ip < inputMappings.Length)
            {
                var b = inputMappings[ip++];
                if (b == ';')
                {
                    commit();
                    _mappings.Append(';');
                    inOutputCol = 0;
                    lastOutputCol = 0;
                    outputLine++;
                }
                else if (b == ',')
                {
                    commit();
                    _mappings.Append(',');
                }
                else
                {
                    if (b >= 128) throw new Exception("Invalid sourceMap");
                    b = char2int[b];
                    if (b > 63) throw new Exception("Invalid sourceMap");
                    value += (b & 31) << shift;
                    if ((b & 32) != 0)
                    {
                        shift += 5;
                    }
                    else
                    {
                        var shouldNegate = (value & 1) != 0;
                        value >>= 1;
                        if (shouldNegate) value = -value;
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
            if (outputLine < sourceLines)
            {
                _mappings.Append(';', sourceLines - outputLine);
            }
        }
    }
}
