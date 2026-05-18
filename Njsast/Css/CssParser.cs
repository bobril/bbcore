using System;
using System.Collections.Generic;
using System.Text;
using Njsast.Reader;
using Njsast.SourceMap;

namespace Njsast.Css;

public sealed class CssParser
{
    readonly string _input;
    readonly string? _sourceFile;
    int _index;
    int _line;
    int _column;

    CssParser(string input, CssParserOptions? options)
    {
        _input = input;
        _sourceFile = options?.SourceFile;
    }

    public static CssStylesheet Parse(string input, CssParserOptions? options = null)
    {
        return new CssParser(input, options).ParseStylesheet();
    }

    CssStylesheet ParseStylesheet()
    {
        var start = CurrentPosition();
        var sheet = new CssStylesheet(_sourceFile, start, start);
        ParseNodes(sheet.Nodes, stopAtCloseBrace: false);
        sheet.End = CurrentPosition();
        return sheet;
    }

    void ParseNodes(List<CssNode> nodes, bool stopAtCloseBrace)
    {
        while (!Eof)
        {
            SkipWhitespace();
            if (Eof) break;
            if (Peek == '}')
            {
                if (!stopAtCloseBrace) Throw("Unexpected }");
                Read();
                return;
            }

            if (StartsWith("/*"))
            {
                nodes.Add(ParseComment());
                continue;
            }

            nodes.Add(Peek == '@' ? ParseAtRule() : ParseRuleOrDeclaration());
        }

        if (stopAtCloseBrace) Throw("Unclosed block");
    }

    CssNode ParseComment()
    {
        var start = CurrentPosition();
        Read();
        Read();
        var textStart = _index;
        while (!Eof && !StartsWith("*/")) Read();
        if (Eof) Throw("Unclosed comment", start);
        var text = _input[textStart.._index];
        Read();
        Read();
        return new CssComment(_sourceFile, start, CurrentPosition(), text);
    }

    CssNode ParseAtRule()
    {
        var start = CurrentPosition();
        Read();
        var nameStart = _index;
        while (!Eof && IsNameChar(Peek)) Read();
        if (nameStart == _index) Throw("At-rule without name", start);
        var name = _input[nameStart.._index];
        var preludeStart = _index;
        var end = ReadPrelude(out var terminator);
        var parameters = _input[preludeStart..end].Trim();
        if (terminator == ';')
            return new CssAtRule(_sourceFile, start, CurrentPosition(), name, parameters);
        if (terminator != '{') Throw("Expected { or ;");
        var atRule = new CssAtRule(_sourceFile, start, CurrentPosition(), name, parameters) { Nodes = new() };
        ParseNodes(atRule.Nodes, stopAtCloseBrace: true);
        atRule.End = CurrentPosition();
        return atRule;
    }

    CssNode ParseRuleOrDeclaration()
    {
        var start = CurrentPosition();
        var preludeStart = _index;
        var end = ReadPrelude(out var terminator);
        var head = _input[preludeStart..end].Trim();
        if (terminator == '{')
        {
            if (FindTopLevelColon(head) >= 0)
            {
                end = ReadDeclarationValueBlockTail();
                head = _input[preludeStart..end].Trim();
                return CreateDeclaration(start, CurrentPosition(), head);
            }

            var rule = new CssRule(_sourceFile, start, CurrentPosition(), head);
            ParseNodes(rule.Nodes, stopAtCloseBrace: true);
            rule.End = CurrentPosition();
            return rule;
        }

        if (terminator == ';' || terminator == '}' || terminator == '\0')
            return CreateDeclaration(start, CurrentPosition(), head);

        Throw("Expected { or ;");
        throw new InvalidOperationException();
    }

    CssDeclaration CreateDeclaration(Position start, Position end, string text)
    {
        var colon = FindTopLevelColon(text);
        if (colon < 0) Throw("Expected declaration colon", start);
        var property = text[..colon].Trim();
        var value = text[(colon + 1)..].Trim();
        if (property.Length == 0) Throw("Expected declaration property", start);
        var important = EndsWithImportant(value);
        if (important)
            value = value[..LastImportantStart(value)].TrimEnd();
        return new CssDeclaration(_sourceFile, start, end, property, value, important);
    }

    int ReadPrelude(out char terminator)
    {
        var square = 0;
        var paren = 0;
        while (!Eof)
        {
            if (StartsWith("/*"))
            {
                ReadCommentBody();
                continue;
            }

            var ch = Peek;
            if (ch is '"' or '\'')
            {
                ReadString(ch);
                continue;
            }

            if (ch == '\\')
            {
                ReadEscape();
                continue;
            }

            if (ch == '[') square++;
            else if (ch == ']') square--;
            else if (ch == '(') paren++;
            else if (ch == ')') paren--;
            else if (square == 0 && paren == 0 && (ch == '{' || ch == ';' || ch == '}'))
            {
                terminator = ch;
                var end = _index;
                if (ch != '}') Read();
                return end;
            }

            Read();
        }

        terminator = '\0';
        return _index;
    }

    int ReadDeclarationValueBlockTail()
    {
        var curly = 1;
        var square = 0;
        var paren = 0;
        while (!Eof)
        {
            if (StartsWith("/*"))
            {
                ReadCommentBody();
                continue;
            }

            var ch = Peek;
            if (ch is '"' or '\'')
            {
                ReadString(ch);
                continue;
            }

            if (ch == '\\')
            {
                ReadEscape();
                continue;
            }

            if (ch == '[') square++;
            else if (ch == ']') square--;
            else if (ch == '(') paren++;
            else if (ch == ')') paren--;
            else if (square == 0 && paren == 0)
            {
                if (ch == '{') curly++;
                else if (ch == '}')
                {
                    curly--;
                    Read();
                    if (curly == 0) break;
                    continue;
                }
            }

            Read();
        }

        while (!Eof)
        {
            var ch = Peek;
            if (ch == ';')
            {
                var end = _index;
                Read();
                return end;
            }

            if (ch == '}')
                return _index;

            Read();
        }

        return _index;
    }

    public static void ResolveSourceMap(CssNode node, Njsast.SourceMap.SourceMap sourceMap)
    {
        foreach (var child in node.Children())
            ResolveSourceMap(child, sourceMap);
        var start = sourceMap.FindPosition(node.Start.Line + 1, node.Start.Column + 1);
        var end = sourceMap.FindPosition(node.End.Line + 1, node.End.Column + 1);
        node.Source = start.SourceName == "" ? null : start.SourceName;
        node.Start = new(start.Line - 1, start.Col - 1, -1);
        node.End = new(end.Line - 1, end.Col - 1, -1);
    }

    static bool EndsWithImportant(string value)
    {
        var pos = LastImportantStart(value);
        return pos >= 0 && value[pos..].Trim().Equals("!important", StringComparison.OrdinalIgnoreCase);
    }

    static int LastImportantStart(string value)
    {
        return value.LastIndexOf("!important", StringComparison.OrdinalIgnoreCase);
    }

    static int FindTopLevelColon(string text)
    {
        var square = 0;
        var paren = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch is '"' or '\'')
            {
                i = SkipString(text, i);
                continue;
            }
            if (ch == '[') square++;
            else if (ch == ']') square--;
            else if (ch == '(') paren++;
            else if (ch == ')') paren--;
            else if (ch == ':' && square == 0 && paren == 0) return i;
        }
        return -1;
    }

    static int SkipString(string text, int start)
    {
        var quote = text[start];
        for (var i = start + 1; i < text.Length; i++)
        {
            if (text[i] == '\\')
            {
                i++;
                continue;
            }
            if (text[i] == quote) return i;
        }
        return text.Length - 1;
    }

    void ReadCommentBody()
    {
        Read();
        Read();
        while (!Eof && !StartsWith("*/")) Read();
        if (Eof) Throw("Unclosed comment");
        Read();
        Read();
    }

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
        Throw("Unclosed string");
    }

    void ReadEscape()
    {
        Read();
        if (!Eof) Read();
    }

    void SkipWhitespace()
    {
        while (!Eof && char.IsWhiteSpace(Peek)) Read();
    }

    bool StartsWith(string text) => _input.AsSpan(_index).StartsWith(text, StringComparison.Ordinal);

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

    void Throw(string message) => Throw(message, CurrentPosition());
    static void Throw(string message, Position position) => throw new CssParseException(message, position);

    static bool IsNameChar(char ch) => char.IsLetterOrDigit(ch) || ch is '-' or '_';
}

public static class CssUrlRewriter
{
    public static void Rewrite(CssStylesheet stylesheet, Func<string, string, string> rewrite)
    {
        Walk(stylesheet, rewrite);
    }

    static void Walk(CssNode node, Func<string, string, string> rewrite)
    {
        if (node is CssDeclaration declaration)
            declaration.Value = RewriteUrls(declaration.Value, declaration.Source ?? "", rewrite);
        else if (node is CssAtRule atRule)
            atRule.Parameters = RewriteUrls(atRule.Parameters, atRule.Source ?? "", rewrite);
        foreach (var child in node.Children())
            Walk(child, rewrite);
    }

    static string RewriteUrls(string text, string from, Func<string, string, string> rewrite)
    {
        var result = new StringBuilder(text.Length);
        var pos = 0;
        while (pos < text.Length)
        {
            var found = text.IndexOf("url(", pos, StringComparison.OrdinalIgnoreCase);
            if (found < 0)
            {
                result.Append(text, pos, text.Length - pos);
                break;
            }
            result.Append(text, pos, found - pos);
            var valueStart = found + 4;
            var end = FindUrlEnd(text, valueStart);
            if (end < 0)
            {
                result.Append(text, found, text.Length - found);
                break;
            }
            var raw = text[valueStart..end].Trim();
            var quote = raw.Length >= 2 && (raw[0] == '"' || raw[0] == '\'') && raw[^1] == raw[0] ? raw[0] : '\0';
            var url = quote == '\0' ? raw : raw[1..^1];
            var nextUrl = url.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ? url : rewrite(url, from);
            result.Append("url(");
            if (quote != '\0') result.Append(quote);
            result.Append(nextUrl);
            if (quote != '\0') result.Append(quote);
            result.Append(')');
            pos = end + 1;
        }
        return result.ToString();
    }

    static int FindUrlEnd(string text, int start)
    {
        var quote = '\0';
        for (var i = start; i < text.Length; i++)
        {
            var ch = text[i];
            if (quote != '\0')
            {
                if (ch == '\\') i++;
                else if (ch == quote) quote = '\0';
                continue;
            }
            if (ch is '"' or '\'') quote = ch;
            else if (ch == ')') return i;
        }
        return -1;
    }
}
