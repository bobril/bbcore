using System;
using System.Collections.Generic;
using Njsast.Utils;

namespace Njsast.SourceMap
{
    public class SourceMapBuilder
    {
        StructList<char> _content;
        StructList<string> _sources;
        StructList<char> _mappings;
        int _lastSourceIndex;
        int _lastSourceLine;
        int _lastSourceCol;

        public const string Int2Char = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/=";

        // Just ASCII so length is 128
        // var b = new StringBuilder();
        // b.Append('\xff', 128);
        // for (var i = 0; i < a.Length; i++) b[int2Char[i]] = (char) i;
        // b.toString()
        public const string Char2Int =
            "ÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿ>ÿÿÿ?456789:;<=ÿÿÿ@ÿÿÿ\0\u0001\u0002\u0003\u0004\u0005\u0006\a\b\t\n\v\f\r\u000e\u000f\u0010\u0011\u0012\u0013\u0014\u0015\u0016\u0017\u0018\u0019ÿÿÿÿÿÿ\u001a\u001b\u001c\u001d\u001e\u001f !\"#$%&'()*+,-./0123ÿÿÿÿÿ";

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
            return new string(_content.AsSpan());
        }

        public SourceMap Build(string subtractDir, string srcRoot)
        {
            var sources = new List<string>((int)_sources.Count);
            foreach (var s in _sources) sources.Add(PathUtils.Subtract(s, subtractDir));

            return new SourceMap
            {
                sourceRoot = srcRoot,
                sources = sources,
                mappings = new string(_mappings.AsSpan())
            };
        }

        static int CountNL(ReadOnlySpan<char> content)
        {
            var result = 0;
            for (var i = 0; i < content.Length; i++)
            {
                if (content[i] == '\n') result++;
            }

            return result;
        }

        public static bool EndsWithNL(in ReadOnlySpan<char> content)
        {
            if (content.Length == 0) return false;
            return content[content.Length - 1] == '\n';
        }

        public void AddText(ReadOnlySpan<char> content)
        {
            _content.AddRange(content);
            var lines = CountNL(content);
            if (!EndsWithNL(content))
            {
                lines++;
                _content.Add('\n');
            }

            _mappings.RepeatAdd(';', (uint)lines);
        }

        static void addVLQ(ref StructList<char> that, int num)
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

                that.Add(Int2Char[clamped]);
            } while (num > 0);
        }

        public void AddSource(ReadOnlySpan<char> content, SourceMap sourceMap = null)
        {
            if (sourceMap == null)
            {
                AddText(content);
                return;
            }

            _content.AddRange(content);
            var sourceLines = CountNL(content);
            if (!EndsWithNL(content))
            {
                sourceLines++;
                _content.Add('\n');
            }

            var sourceRemap = new StructList<int>();
            sourceMap.sources.ForEach(v =>
            {
                var pos = _sources.IndexOf(v);
                if (pos < 0)
                {
                    pos = (int)_sources.Count;
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
            var valPos = 0;

            void Commit()
            {
                if (valPos == 0) return;
                addVLQ(ref _mappings, inOutputCol - lastOutputCol);
                lastOutputCol = inOutputCol;
                if (valPos == 1)
                {
                    valPos = 0;
                    return;
                }

                var outSourceIndex = sourceRemap[(uint)inSourceIndex];
                addVLQ(ref _mappings, outSourceIndex - _lastSourceIndex);
                _lastSourceIndex = outSourceIndex;
                addVLQ(ref _mappings, inSourceLine - _lastSourceLine);
                _lastSourceLine = inSourceLine;
                addVLQ(ref _mappings, inSourceCol - _lastSourceCol);
                _lastSourceCol = inSourceCol;
                valPos = 0;
            }

            while ((uint)ip < inputMappings.Length)
            {
                var b = inputMappings[ip++];
                if (b == ';')
                {
                    Commit();
                    _mappings.Add(';');
                    inOutputCol = 0;
                    lastOutputCol = 0;
                    outputLine++;
                }
                else if (b == ',')
                {
                    Commit();
                    _mappings.Add(',');
                }
                else
                {
                    if (b >= 128) throw new Exception("Invalid sourceMap");
                    b = Char2Int[b];
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
                        switch (valPos)
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

                        valPos++;
                        value = shift = 0;
                    }
                }
            }

            Commit();
            if (outputLine < sourceLines)
            {
                _mappings.RepeatAdd(';', (uint)(sourceLines - outputLine));
            }
        }

        public ISourceAdder CreateSourceAdder(string content, SourceMap sourceMap = null)
        {
            if (sourceMap == null)
                return new PlainTextAdder(this, content);
            return new SourceMapTextAdder(this, content, sourceMap);
        }

        public class PlainTextAdder : ISourceAdder
        {
            readonly SourceMapBuilder _owner;
            readonly string _content;
            int _line;
            int _col;
            int _index;
            bool _needNewLine;

            public PlainTextAdder(SourceMapBuilder owner, string content)
            {
                _owner = owner;
                _content = content;
            }

            public void Add(int fromLine, int fromCol, int toLine, int toCol)
            {
                SeekTo(fromLine, fromCol);
                var start = _index;
                SeekTo(toLine, toCol);
                var end = _index;
                Add(_content.AsSpan(start, end - start));
            }

            public void Add(ReadOnlySpan<char> text)
            {
                if (text.Length == 0) return;
                _owner._content.AddRange(text);
                var lines = CountNL(text);
                _needNewLine = !EndsWithNL(text);
                _owner._mappings.RepeatAdd(';', (uint)lines);
            }

            void SeekTo(int line, int col)
            {
                if (line < _line || line == _line && col < _col)
                {
                    _line = 0;
                    _col = 0;
                    _index = 0;
                }

                while (line > _line)
                {
                    while (_index < _content.Length && _content[_index] != '\n') _index++;
                    if (_index < _content.Length) _index++;
                    _line++;
                    _col = 0;
                }

                while (col > _col)
                {
                    while (_index < _content.Length && _content[_index] != '\n') _index++;
                    _col++;
                }
            }
        }

        public class SourceMapTextAdder : ISourceAdder
        {
            readonly SourceMapBuilder _owner;
            readonly SourceMapIterator _iterator;
            StructList<int> _sourceRemap;
            int _lastOutputLastCol;
            int _lastOutputCol;
            int _lastOutputColEnd;
            int _lastSourceIndex;
            int _lastSourceLine;
            int _lastSourceCol;
            bool _needComma;

            public SourceMapTextAdder(SourceMapBuilder owner, string content, SourceMap sourceMap)
            {
                _owner = owner;
                _iterator = new SourceMapIterator(content, sourceMap);
                _sourceRemap = new StructList<int>();
                ref var ownerSources = ref owner._sources;
                foreach (var v in sourceMap.sources)
                {
                    var pos = ownerSources.IndexOf(v);
                    if (pos < 0)
                    {
                        pos = (int)ownerSources.Count;
                        ownerSources.Add(v);
                    }

                    _sourceRemap.Add(pos);
                }

                _lastSourceIndex = -1;
                _lastSourceLine = -1;
                _lastSourceCol = -1;
            }

            public void Add(int fromLine, int fromCol, int toLine, int toCol)
            {
                _iterator.SeekTo(fromLine, fromCol);
                bool allowMerge;
                if (fromLine == toLine && (toCol <= _iterator.ColEnd || _iterator.TillEndOfLine))
                {
                    if (fromCol < toCol)
                    {
                        allowMerge = _iterator.ContentSpan[fromCol - _iterator.ColStart] != '.';
                        _owner._content.AddRange(_iterator.ContentSpan.Slice(fromCol - _iterator.ColStart,
                            toCol - fromCol));
                    }
                    else allowMerge = true;
                    Commit(fromCol, toCol, Remap(_iterator.SourceIndex), _iterator.SourceLine,
                        _iterator.SourceCol + fromCol - _iterator.ColStart, allowMerge);
                    return;
                }

                if (_iterator.ColEnd > fromCol)
                {
                    allowMerge = _iterator.ContentSpan[fromCol - _iterator.ColStart] != '.';
                    _owner._content.AddRange(_iterator.ContentSpan.Slice(fromCol - _iterator.ColStart));
                }
                else allowMerge = true;
                Commit(fromCol, _iterator.ColEnd, Remap(_iterator.SourceIndex), _iterator.SourceLine,
                    _iterator.SourceCol + fromCol - _iterator.ColStart, allowMerge);
                if (_iterator.TillEndOfLine)
                {
                    _owner._content.Add('\n');
                    CommitNewLine();
                }

                _iterator.Next();
                while (_iterator.Line < toLine ||
                       _iterator.Line == toLine && _iterator.ColEnd < toCol)
                {
                    if (_iterator.EndOfContent) break;
                    if (_iterator.ColStart < _iterator.ColEnd)
                    {
                        allowMerge = _iterator.ContentSpan[0] != '.';
                        _owner._content.AddRange(_iterator.ContentSpan);
                    }
                    else allowMerge = true;
                    Commit(_iterator.ColStart, _iterator.ColEnd, Remap(_iterator.SourceIndex), _iterator.SourceLine,
                        _iterator.SourceCol, allowMerge);
                    if (_iterator.TillEndOfLine)
                    {
                        _owner._content.Add('\n');
                        CommitNewLine();
                    }

                    _iterator.Next();
                }

                if (!_iterator.EndOfContent)
                {
                    if (_iterator.ContentSpan.Length > 0)
                    {
                        allowMerge = _iterator.ContentSpan[0] != '.';
                        _owner._content.AddRange(_iterator.ContentSpan.Slice(0, toCol - _iterator.ColStart));
                    }
                    else allowMerge = true;
                    Commit(_iterator.ColStart, toCol, Remap(_iterator.SourceIndex), _iterator.SourceLine,
                        _iterator.SourceCol, allowMerge);
                }
            }

            void CommitNewLine()
            {
                if (_lastOutputColEnd > _lastOutputCol && _lastSourceIndex >= 0)
                {
                    CommitLast();
                }
                _owner._mappings.Add(';');
                _lastOutputLastCol = 0;
                _lastOutputCol = 0;
                _lastOutputColEnd = 0;
                _lastSourceIndex = -1;
                _needComma = false;
            }

            void CommitLast()
            {
                if (_needComma)
                {
                    _owner._mappings.Add(',');
                }
                else
                {
                    if (_lastOutputCol == 0 && _lastSourceIndex == -1)
                    {
                        _lastOutputCol = _lastOutputColEnd;
                        return;
                    }
                }
                addVLQ(ref _owner._mappings, _lastOutputCol - _lastOutputLastCol);
                _lastOutputLastCol = _lastOutputCol;
                _lastOutputCol = _lastOutputColEnd;

                if (_lastSourceIndex != -1)
                {
                    addVLQ(ref _owner._mappings, _lastSourceIndex - _owner._lastSourceIndex);
                    _owner._lastSourceIndex = _lastSourceIndex;
                    addVLQ(ref _owner._mappings, _lastSourceLine - _owner._lastSourceLine);
                    _owner._lastSourceLine = _lastSourceLine;
                    addVLQ(ref _owner._mappings, _lastSourceCol - _owner._lastSourceCol);
                    _owner._lastSourceCol = _lastSourceCol;

                }
                _needComma = true;
            }

            void Commit(int fromCol, int toCol, int sourceIndex, int sourceLine, int sourceCol, bool allowMerge)
            {
                if (_lastOutputColEnd == _lastOutputCol)
                {
                    _lastOutputColEnd += toCol - fromCol;
                    _lastSourceIndex = sourceIndex;
                    _lastSourceLine = sourceLine;
                    _lastSourceCol = sourceCol;
                    return;
                }

                if (_lastSourceIndex == sourceIndex && sourceIndex == -1)
                {
                    _lastOutputColEnd += toCol - fromCol;
                    return;
                }

                if (allowMerge && _lastSourceIndex == sourceIndex && _lastSourceLine == sourceLine &&
                    _lastSourceCol + _lastOutputColEnd - _lastOutputCol == sourceCol)
                {
                    _lastOutputColEnd += toCol - fromCol;
                    return;
                }

                CommitLast();
                _lastOutputColEnd += toCol - fromCol;
                _lastSourceIndex = sourceIndex;
                _lastSourceLine = sourceLine;
                _lastSourceCol = sourceCol;
            }

            int Remap(int sourceIndex)
            {
                if (sourceIndex < 0)
                    return -1;
                return _sourceRemap[(uint)sourceIndex];
            }

            public void Add(ReadOnlySpan<char> text)
            {
                while (text.Length > 0)
                {
                    var nl = text.IndexOf('\n');
                    if (nl == -1)
                    {
                        _owner._content.AddRange(text);
                        Commit(0, text.Length, -1, -1, -1, true);
                        return;
                    }

                    _owner._content.AddRange(text.Slice(0, nl + 1));
                    Commit(0, nl, -1, -1, -1, true);
                    CommitNewLine();
                    text = text.Slice(nl + 1);
                }
            }
        }
    }
}
