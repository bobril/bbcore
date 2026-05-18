using System;
using System.Collections.Generic;
using Njsast.Reader;

namespace Njsast.Css;

public enum CssTokenType
{
    Identifier,
    AtKeyword,
    String,
    Comment,
    Whitespace,
    Delimiter,
    EndOfFile
}

public readonly record struct CssToken(CssTokenType Type, string Text, Position Start, Position End);

public sealed class CssTokenizer
{
    readonly string _input;
    int _index;
    int _line;
    int _column;

    public CssTokenizer(string input)
    {
        _input = input;
    }

    public IEnumerable<CssToken> Tokenize()
    {
        while (!Eof)
        {
            var start = CurrentPosition();
            var ch = Peek;
            if (char.IsWhiteSpace(ch))
            {
                while (!Eof && char.IsWhiteSpace(Peek)) Read();
                yield return Token(CssTokenType.Whitespace, start);
            }
            else if (StartsWith("/*"))
            {
                Read();
                Read();
                while (!Eof && !StartsWith("*/")) Read();
                if (!Eof)
                {
                    Read();
                    Read();
                }
                yield return Token(CssTokenType.Comment, start);
            }
            else if (ch is '"' or '\'')
            {
                ReadString(ch);
                yield return Token(CssTokenType.String, start);
            }
            else if (ch == '@')
            {
                Read();
                while (!Eof && IsNameChar(Peek)) Read();
                yield return Token(CssTokenType.AtKeyword, start);
            }
            else if (IsNameChar(ch) || ch == '#')
            {
                Read();
                while (!Eof && (IsNameChar(Peek) || Peek == '#')) Read();
                yield return Token(CssTokenType.Identifier, start);
            }
            else
            {
                Read();
                yield return Token(CssTokenType.Delimiter, start);
            }
        }

        yield return new CssToken(CssTokenType.EndOfFile, "", CurrentPosition(), CurrentPosition());
    }

    CssToken Token(CssTokenType type, Position start) => new(type, _input[start.Index.._index], start, CurrentPosition());

    void ReadString(char quote)
    {
        Read();
        while (!Eof)
        {
            var ch = Read();
            if (ch == '\\')
            {
                if (!Eof) Read();
                continue;
            }
            if (ch == quote) return;
        }
    }

    bool StartsWith(string text) => _input.AsSpan(_index).StartsWith(text, System.StringComparison.Ordinal);

    char Read()
    {
        var ch = _input[_index++];
        if (ch == '\n')
        {
            _line++;
            _column = 0;
        }
        else
        {
            _column++;
        }
        return ch;
    }

    Position CurrentPosition() => new(_line, _column, _index);
    bool Eof => _index >= _input.Length;
    char Peek => _input[_index];
    static bool IsNameChar(char ch) => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '\\';
}
