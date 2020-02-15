using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Njsast.Ast;
using Njsast.Reader;
using Njsast.Utils;

namespace Njsast.SourceMap
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class SourceMap
    {
        public SourceMap()
        {
            sources = new List<string>();
        }
        public SourceMap(List<string> sources)
        {
            version = 3;
            this.sources = sources;
        }

        public int version { get; set; }
        public string? file { get; set; }
        public string sourceRoot { get; set; } = string.Empty;
        public List<string> sources { get; set; }
        public List<string>? sourcesContent { get; set; }
        public List<string>? names { get; set; }
        public string mappings { get; set; } = string.Empty;

        public const uint CacheLineSkip = 8;

        [JsonIgnore]
        // cache for every multiply of CacheLineSkip output lines
        SourceMapPositionTrinity[]? _searchCache;

        struct SourceMapPositionTrinity
        {
            public SourceMapPositionTrinity(int pos, int index, int line, int col)
            {
                Pos = pos;
                Index = index;
                Line = line;
                Col = col;
            }

            public readonly int Pos;
            public readonly int Index;
            public readonly int Line;
            public readonly int Col;
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this,
                new JsonSerializerSettings() {NullValueHandling = NullValueHandling.Ignore});
        }

        public static SourceMap Empty()
        {
            return new SourceMap
            {
                mappings = ""
            };
        }

        public static SourceMap Identity(string content, string fileName)
        {
            var sb = new StringBuilder();
            sb.Append("AAAA");
            var endsWithNL = SourceMapBuilder.EndsWithNewLine(content);
            var len = content.Length - (endsWithNL ? 1 : 0);
            for (var i = 0; i < len; i++)
                if (content[i] == '\n')
                    sb.Append(";AACA");
            if (endsWithNL)
                sb.Append(";");
            return new SourceMap(new List<string> {fileName})
            {
                mappings = sb.ToString()
            };
        }

        public static SourceMap Parse(string content, string dir)
        {
            var res = JsonConvert.DeserializeObject<SourceMap>(content);
            if (res.version != 3)
                throw new Exception("Invalid Source Map version " + res.version);
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
                if (content[pos] == 10)
                    break;
                pos--;
            }

            if (pos < content.Length - 5)
            {
                if (content.Substring(pos + 1, 3) == "//#")
                    return content.Substring(0, pos < 0 ? 0 : pos);
            }

            return content;
        }

        public void BuildSearchCache()
        {
            var inputMappings = this.mappings;
            var outputLineCount = 1;
            for (var i = 0; i < inputMappings.Length; i++)
            {
                if (inputMappings[i] == ';')
                    outputLineCount++;
            }

            _searchCache = new SourceMapPositionTrinity[outputLineCount / CacheLineSkip];
            var ip = 0;
            var inSourceIndex = 0;
            var inSourceLine = 0;
            var inSourceCol = 0;
            var outputLine = 1;
            var shift = 0;
            var value = 0;
            var valpos = 0;
            var cachePos = 0;
            while (ip < inputMappings.Length)
            {
                var ch = inputMappings[ip++];
                if (ch == ';')
                {
                    valpos = 0;
                    outputLine++;
                    if (outputLine % CacheLineSkip == 0)
                    {
                        _searchCache[cachePos] =
                            new SourceMapPositionTrinity(ip, inSourceIndex, inSourceLine, inSourceCol);
                        cachePos++;
                    }
                }
                else if (ch == ',')
                {
                    valpos = 0;
                }
                else
                {
                    var b = (int) SourceMapBuilder.Char2Int[ch];
                    if (b == 255)
                        throw new Exception("Invalid sourceMap");
                    value += (b & 31) << shift;
                    if ((b & 32) != 0)
                    {
                        shift += 5;
                    }
                    else
                    {
                        var shouldNegate = value & 1;
                        value >>= 1;
                        if (shouldNegate != 0)
                            value = -value;
                        switch (valpos)
                        {
                            case 1:
                                inSourceIndex += value;
                                break;
                            case 2:
                                inSourceLine += value;
                                break;
                            case 3:
                                inSourceCol += value;
                                break;
                        }

                        valpos++;
                        value = shift = 0;
                    }
                }
            }
        }


        class DerefVisitor : TreeWalker
        {
            readonly SourceMap _sourceMap;

            public DerefVisitor(SourceMap sourceMap)
            {
                _sourceMap = sourceMap;
            }

            protected override void Visit(AstNode node)
            {
                var start = _sourceMap.FindPosition(node.Start.Line + 1, node.Start.Column + 1);
                var end = _sourceMap.FindPosition(node.End.Line + 1, node.End.Column + 1);
                if (!ReferenceEquals(start.SourceName, end.SourceName))
                {
                    if (end.SourceName != "")
                    {
                        return;
                    }

                    end.Line = start.Line + node.End.Line - node.Start.Line;
                    end.Col = start.Col + node.End.Column - node.Start.Column;
                }
                node.Source = start.SourceName == "" ? null : start.SourceName;
                node.Start = new Position(start.Line - 1, start.Col - 1, -1);
                node.End = new Position(end.Line - 1, end.Col - 1, -1);
            }
        }

        public void ResolveInAst(AstNode node)
        {
            new DerefVisitor(this).Walk(node);
        }

        public SourceCodePosition FindPosition(int line, int col)
        {
            if (_searchCache == null)
            {
                BuildSearchCache();
            }

            var inputMappings = mappings;
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
            var lastSourceIndex = -1;
            var lastSourceLine = 0;
            var lastSourceCol = 0;
            var res = new SourceCodePosition();
            if (line > CacheLineSkip)
            {
                var pos = (uint) (line - 1) / CacheLineSkip;
                ref var entry = ref _searchCache![pos - 1];
                outputLine = (int) (pos * CacheLineSkip);
                ip = entry.Pos;
                inSourceIndex = entry.Index;
                inSourceLine = entry.Line;
                inSourceCol = entry.Col;
                lastSourceIndex = inSourceIndex;
                lastSourceLine = inSourceLine;
                lastSourceCol = inSourceCol;
            }

            bool commit()
            {
                if (outputLine > line)
                    return true;
                if (valpos == 0)
                {
                    lastSourceIndex = -1;
                    return false;
                }

                if (outputLine == line && lastOutputCol <= col && col <= inOutputCol)
                {
                    if (lastSourceIndex < 0)
                        return false;
                    res.SourceName = sources[lastSourceIndex];
                    res.Line = lastSourceLine + 1;
                    res.Col = lastSourceCol + col - lastOutputCol;
                    return true;
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
                        return true;
                    }
                }

                lastOutputCol = inOutputCol;
                valpos = 0;
                return false;
            }

            while (ip < inputMappings.Length)
            {
                var ch = inputMappings[ip++];
                if (ch == ';')
                {
                    if (commit())
                        return res;
                    if (outputLine == line && lastOutputCol <= col)
                    {
                        if (lastSourceIndex < 0)
                            return res;
                        res.SourceName = sources[lastSourceIndex];
                        res.Line = lastSourceLine + 1;
                        res.Col = lastSourceCol + col - lastOutputCol;
                        return res;
                    }

                    inOutputCol = 0;
                    lastOutputCol = 0;
                    outputLine++;
                }
                else if (ch == ',')
                {
                    if (commit())
                        return res;
                }
                else
                {
                    var b = (int) SourceMapBuilder.Char2Int[ch];
                    if (b == 255)
                        throw new Exception("Invalid sourceMap");
                    value += (b & 31) << shift;
                    if ((b & 32) != 0)
                    {
                        shift += 5;
                    }
                    else
                    {
                        var shouldNegate = value & 1;
                        value >>= 1;
                        if (shouldNegate != 0)
                            value = -value;
                        switch (valpos)
                        {
                            case 0:
                                inOutputCol += value;
                                break;
                            case 1:
                                inSourceIndex += value;
                                break;
                            case 2:
                                inSourceLine += value;
                                break;
                            case 3:
                                inSourceCol += value;
                                break;
                        }

                        valpos++;
                        value = shift = 0;
                    }
                }
            }

            if (!commit())
            {
                if (lastSourceIndex >= 0)
                {
                    res.SourceName = sources[lastSourceIndex];
                    res.Line = lastSourceLine + 1;
                    res.Col = lastSourceCol + 1;
                }
            }
            return res;
        }
    }

    public class SourceMapIterator
    {
        readonly string _content;
        readonly string _mappings;
        int _ip;
        int _inOutputCol;
        int _inSourceIndex;
        int _inSourceLine;
        int _inSourceCol;
        int _lastOutputCol;
        int _lastSourceIndex;
        int _lastSourceLine;
        int _lastSourceCol;
        int _line;
        int _index;
        bool _nextIsNewLine;
        bool _inNoSource;
        bool _lastNoSource;

        public SourceMapIterator(string content, SourceMap sourceMap)
        {
            _content = content;
            _mappings = sourceMap.mappings;
            Reset();
        }

        void Reset()
        {
            _index = 0;
            _line = 0;
            _ip = 0;
            _inOutputCol = 0;
            _inSourceIndex = 0;
            _inSourceLine = 0;
            _inSourceCol = 0;
            _inNoSource = true;
            _lastOutputCol = 0;
            _lastSourceIndex = 0;
            _lastSourceLine = 0;
            _lastSourceCol = 0;
            _lastNoSource = true;
            _nextIsNewLine = false;
            Next();
        }

        public void Next()
        {
            _index += _inOutputCol - _lastOutputCol;
            again:
            _lastOutputCol = _inOutputCol;
            _lastSourceIndex = _inSourceIndex;
            _lastSourceLine = _inSourceLine;
            _lastSourceCol = _inSourceCol;
            _lastNoSource = _inNoSource;

            if (_nextIsNewLine)
            {
                _nextIsNewLine = false;
                _inOutputCol = 0;
                _lastOutputCol = 0;
                _line++;
                if (_index < _content.Length)
                {
                    if (_content[_index] == '\r') _index++;
                    _index++;
                }

                _lastNoSource = true;
                _inNoSource = true;
            }

            if (_ip >= _mappings.Length || _mappings[_ip] == ';')
            {
                _nextIsNewLine = true;
                var newLineIndex = _index;
                while (newLineIndex < _content.Length && _content[newLineIndex] != '\n') newLineIndex++;
                _inOutputCol = _lastOutputCol + newLineIndex - _index;
                if (_index < _content.Length && _inOutputCol > _lastOutputCol &&
                    _content[_index + _inOutputCol - _lastOutputCol - 1] == '\r')
                {
                    _inOutputCol--;
                }

                _ip++;
                return;
            }

            var value = 0;
            var shift = 0;
            var valPos = 0;
            while (_ip < _mappings.Length)
            {
                var ch = _mappings[_ip++];
                if (ch == ';')
                {
                    _ip--;
                    _inNoSource = valPos <= 1;
                    if (_inOutputCol == _lastOutputCol) goto again;
                    return;
                }

                if (ch == ',')
                {
                    if (valPos == 0)
                        continue;
                    _inNoSource = valPos <= 1;
                    if (_inOutputCol == _lastOutputCol) goto again;
                    return;
                }

                var b = (int) SourceMapBuilder.Char2Int[ch];
                if (b == 255)
                    throw new Exception("Invalid sourceMap");
                value += (b & 31) << shift;
                if ((b & 32) != 0)
                {
                    shift += 5;
                }
                else
                {
                    var shouldNegate = value & 1;
                    value >>= 1;
                    if (shouldNegate != 0)
                        value = -value;
                    switch (valPos)
                    {
                        case 0:
                            _inOutputCol += value;
                            break;
                        case 1:
                            _inSourceIndex += value;
                            break;
                        case 2:
                            _inSourceLine += value;
                            break;
                        case 3:
                            _inSourceCol += value;
                            break;
                    }

                    valPos++;
                    value = shift = 0;
                }
            }

            _inNoSource = valPos <= 1;
            if (_inOutputCol == _lastOutputCol) goto again;
        }

        public void SeekTo(int line, int col)
        {
            if (line < _line || line == _line && col < _inOutputCol)
            {
                Reset();
            }

            while (line > _line)
            {
                if (EndOfContent) break;
                Next();
            }

            if (line == _line)
            {
                while ((col > _inOutputCol) && !_nextIsNewLine)
                {
                    if (EndOfContent) break;
                    Next();
                }
            }
        }

        public bool EndOfContent => _index >= _content.Length;
        public int ColStart => _lastOutputCol;
        public int ColEnd => _inOutputCol;
        public int Line => _line;
        public bool TillEndOfLine => _nextIsNewLine;
        public ReadOnlySpan<char> ContentSpan => _content.AsSpan(_index, _inOutputCol - _lastOutputCol);
        public int SourceIndex => _lastNoSource ? -1 : _lastSourceIndex;
        public int SourceCol => _lastNoSource ? -1 : _lastSourceCol;
        public int SourceLine => _lastNoSource ? -1 : _lastSourceLine;
    }
}
