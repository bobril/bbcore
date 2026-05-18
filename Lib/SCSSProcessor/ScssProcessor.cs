using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Lib.Utils;
using Njsast.Css;
using Njsast.Reader;

namespace Lib.SCSSProcessor;

public class ScssProcessor : IScssProcessor
{
    public ScssProcessor(Lib.ToolsDir.IToolsDir toolsDir)
    {
    }

    public void Dispose()
    {
    }

    public Task<string> ProcessScss(string source, string from, Func<string, string> canonicalize,
        Func<string, string> loader, Action<string> log)
    {
        var ast = Compile(source, from, canonicalize, loader, log);
        CssMinifier.Minify(ast);
        return Task.FromResult(RemoveFinalDeclarationSemicolons(ast.PrintToString(new CssOutputOptions
            { PreserveComments = false })));
    }

    public Task<CssStylesheet> ProcessScssToCssAst(string source, string from, Func<string, string> canonicalize,
        Func<string, string> loader, Action<string> log)
    {
        return Task.FromResult(Compile(source, from, canonicalize, loader, log));
    }

    static CssStylesheet Compile(string source, string from, Func<string, string> canonicalize,
        Func<string, string> loader, Action<string> log)
    {
        var compiler = new NativeScssCompiler(source, from, canonicalize, loader, log);
        return compiler.Compile();
    }

    static string RemoveFinalDeclarationSemicolons(string css)
    {
        return NormalizeSimpleSingleQuotedStrings(css.Replace(";}", "}", StringComparison.Ordinal));
    }

    static string NormalizeSimpleSingleQuotedStrings(string css)
    {
        var result = new StringBuilder(css.Length);
        for (var i = 0; i < css.Length; i++)
        {
            if (css[i] != '\'')
            {
                result.Append(css[i]);
                continue;
            }

            var end = i + 1;
            var canNormalize = true;
            while (end < css.Length && css[end] != '\'')
            {
                if (css[end] == '"' || css[end] == '\\')
                    canNormalize = false;
                end++;
            }

            if (end >= css.Length || !canNormalize)
            {
                result.Append(css[i]);
                continue;
            }

            result.Append('"');
            for (var j = i + 1; j < end; j++)
                result.Append(css[j]);
            result.Append('"');
            i = end;
        }

        return result.ToString();
    }

    sealed class NativeScssCompiler
    {
        readonly string _source;
        readonly string _from;
        readonly Func<string, string> _canonicalize;
        readonly Func<string, string> _loader;
        readonly Action<string> _log;
        readonly Dictionary<string, string> _variables;
        int _index;
        int _line;
        int _column;

        public NativeScssCompiler(string source, string from, Func<string, string> canonicalize,
            Func<string, string> loader, Action<string> log, Dictionary<string, string>? variables = null)
        {
            _source = source;
            _from = from;
            _canonicalize = canonicalize;
            _loader = loader;
            _log = log;
            _variables = variables ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }

        public CssStylesheet Compile()
        {
            var stylesheet = new CssStylesheet(_from, new Position(0, 0, 0), new Position());
            ParseStatements(stylesheet.Nodes, new List<string>(), null);
            stylesheet.End = CurrentPosition();
            return stylesheet;
        }

        void ParseStatements(List<CssNode> output, List<string> selectors, CssAtRule? atRule)
        {
            while (!Eof)
            {
                SkipIgnored();
                if (Eof) return;
                if (Peek == '}')
                {
                    Read();
                    return;
                }

                if (Peek == '$')
                {
                    ParseVariable();
                    continue;
                }

                if (Peek == '@')
                {
                    ParseAtRule(output, selectors, atRule);
                    continue;
                }

                ParseRuleOrDeclaration(output, selectors, atRule);
            }
        }

        void ParseVariable()
        {
            Read();
            var nameStart = _index;
            while (!Eof && IsNameChar(Peek)) Read();
            var name = _source[nameStart.._index];
            SkipIgnored();
            Expect(':');
            var valueStart = _index;
            ReadUntilTerminator(out _);
            _variables[name] = ResolveValue(_source[valueStart.._index].Trim());
            if (!Eof && Peek == ';') Read();
        }

        void ParseAtRule(List<CssNode> output, List<string> selectors, CssAtRule? parentAtRule)
        {
            var start = CurrentPosition();
            Read();
            var nameStart = _index;
            while (!Eof && IsNameChar(Peek)) Read();
            var name = _source[nameStart.._index];
            var parametersStart = _index;
            var end = ReadUntilTerminator(out var terminator);
            var parameters = ResolveValue(_source[parametersStart..end].Trim());
            if (terminator == ';')
                Read();

            if (name.Equals("import", StringComparison.OrdinalIgnoreCase) && terminator == ';')
            {
                var importUrl = ExtractImportUrl(parameters);
                if (importUrl != null)
                {
                    var canonical = _canonicalize(ResolveImportUrl(importUrl));
                    var importedSource = _loader(canonical);
                    var importedCompiler = new NativeScssCompiler(importedSource, canonical, _canonicalize, _loader,
                        _log, _variables);
                    output.AddRange(importedCompiler.Compile().Nodes);
                    return;
                }
            }

            if (terminator == ';')
            {
                output.Add(new CssAtRule(_from, start, CurrentPosition(), name, parameters));
                return;
            }

            if (terminator != '{')
                throw Error("Expected { or ;");

            var atRule = new CssAtRule(_from, start, CurrentPosition(), name, parameters) { Nodes = new() };
            ParseStatements(atRule.Nodes, selectors, atRule);
            atRule.End = CurrentPosition();
            output.Add(atRule);
        }

        void ParseRuleOrDeclaration(List<CssNode> output, List<string> selectors, CssAtRule? atRule)
        {
            var start = CurrentPosition();
            var headStart = _index;
            var end = ReadUntilTerminator(out var terminator);
            var head = _source[headStart..end].Trim();

            if (terminator == '{')
            {
                var selector = ResolveValue(head);
                var nestedSelectors = CombineSelectors(selectors, selector);
                var nestedOutput = atRule?.Nodes ?? output;
                var rule = new CssRule(_from, start, CurrentPosition(), selectors.Count == 0
                    ? selector
                    : string.Join(",", nestedSelectors));
                nestedOutput.Add(rule);
                ParseNestedBlock(nestedOutput, rule.Nodes, nestedSelectors, atRule);
                if (rule.Nodes.Count == 0)
                    nestedOutput.Remove(rule);
                return;
            }

            if (selectors.Count == 0)
                throw Error("Expected rule");

            var declaration = CreateDeclaration(start, CurrentPosition(), head);
            output.Add(declaration);
            if (!Eof && Peek == ';') Read();
        }

        void ParseNestedBlock(List<CssNode> output, List<CssNode> declarations, List<string> selectors,
            CssAtRule? atRule)
        {
            while (!Eof)
            {
                SkipIgnored();
                if (Eof) return;
                if (Peek == '}')
                {
                    Read();
                    return;
                }

                if (Peek == '$')
                {
                    ParseVariable();
                    continue;
                }

                if (Peek == '@')
                {
                    ParseAtRule(output, selectors, atRule);
                    continue;
                }

                var start = CurrentPosition();
                var headStart = _index;
                var end = ReadUntilTerminator(out var terminator);
                var head = _source[headStart..end].Trim();
                if (terminator == '{')
                {
                    var childSelector = ResolveValue(head);
                    var nestedSelectors = CombineSelectors(selectors, childSelector);
                    if (CanKeepNativeNestedSelector(childSelector))
                    {
                        var rule = new CssRule(_from, start, CurrentPosition(), childSelector);
                        ParseNestedBlock(output, rule.Nodes, nestedSelectors, atRule);
                        if (rule.Nodes.Count > 0)
                            declarations.Add(rule);
                    }
                    else
                    {
                        var nestedDeclarations = new List<CssNode>();
                        ParseNestedBlock(output, nestedDeclarations, nestedSelectors, atRule);
                        if (nestedDeclarations.Count > 0)
                        {
                            var rule = new CssRule(_from, start, CurrentPosition(), string.Join(",", nestedSelectors));
                            rule.Nodes.AddRange(nestedDeclarations);
                            output.Add(rule);
                        }
                    }
                }
                else
                {
                    declarations.Add(CreateDeclaration(start, CurrentPosition(), head));
                    if (!Eof && Peek == ';') Read();
                }
            }
        }

        CssDeclaration CreateDeclaration(Position start, Position end, string text)
        {
            var colon = FindTopLevelColon(text);
            if (colon < 0) throw Error("Expected declaration colon");
            var property = ResolveValue(text[..colon].Trim());
            var value = ResolveValue(text[(colon + 1)..].Trim());
            var important = value.EndsWith("!important", StringComparison.OrdinalIgnoreCase);
            if (important)
                value = value[..^10].TrimEnd();
            return new CssDeclaration(_from, start, end, property, value, important);
        }

        List<string> CombineSelectors(List<string> parents, string childSelectorText)
        {
            var children = SplitSelectorList(childSelectorText);
            if (parents.Count == 0) return children;
            var result = new List<string>();
            foreach (var parent in parents)
            foreach (var child in children)
            {
                result.Add(child.Contains('&', StringComparison.Ordinal)
                    ? child.Replace("&", parent, StringComparison.Ordinal)
                    : parent + " " + child);
            }

            return result;
        }

        static bool CanKeepNativeNestedSelector(string selectorText)
        {
            foreach (var selector in SplitSelectorList(selectorText))
            {
                if (!CanKeepNativeNestedSelectorPart(selector))
                    return false;
            }

            return true;
        }

        static bool CanKeepNativeNestedSelectorPart(string selector)
        {
            var index = selector.IndexOf('&');
            while (index >= 0)
            {
                if (index > 0 && !IsSafeBeforeNestingSelector(selector[index - 1]))
                    return false;
                if (index + 1 < selector.Length && !IsSafeAfterNestingSelector(selector[index + 1]))
                    return false;
                index = selector.IndexOf('&', index + 1);
            }

            return true;
        }

        static bool IsSafeBeforeNestingSelector(char ch)
        {
            return char.IsWhiteSpace(ch) || ch is ',' or '(' or '>' or '+' or '~';
        }

        static bool IsSafeAfterNestingSelector(char ch)
        {
            return char.IsWhiteSpace(ch) || ch is ':' or '[' or '.' or '#' or '>' or '+' or '~' or ',' or ')';
        }

        static List<string> SplitSelectorList(string selector)
        {
            var result = new List<string>();
            var start = 0;
            var paren = 0;
            var square = 0;
            for (var i = 0; i < selector.Length; i++)
            {
                var ch = selector[i];
                if (ch == '(') paren++;
                else if (ch == ')') paren--;
                else if (ch == '[') square++;
                else if (ch == ']') square--;
                else if (ch == ',' && paren == 0 && square == 0)
                {
                    result.Add(selector[start..i].Trim());
                    start = i + 1;
                }
            }

            result.Add(selector[start..].Trim());
            return result;
        }

        string ResolveValue(string value)
        {
            var pos = value.IndexOf("#{", StringComparison.Ordinal);
            while (pos >= 0)
            {
                var end = value.IndexOf('}', pos + 2);
                if (end < 0) break;
                var expression = value[(pos + 2)..end].Trim();
                if (expression.StartsWith("$", StringComparison.Ordinal) &&
                    _variables.TryGetValue(expression[1..], out var replacement))
                {
                    replacement = UnquoteScssString(replacement);
                    value = value[..pos] + replacement + value[(end + 1)..];
                    pos = value.IndexOf("#{", pos + replacement.Length, StringComparison.Ordinal);
                }
                else
                {
                    pos = value.IndexOf("#{", end + 1, StringComparison.Ordinal);
                }
            }

            foreach (var (name, replacement) in _variables)
                value = value.Replace("$" + name, replacement, StringComparison.Ordinal);

            return value;
        }

        string ResolveImportUrl(string importUrl)
        {
            if (Uri.TryCreate(importUrl, UriKind.Absolute, out _))
                return importUrl;
            if (!Uri.TryCreate(_from, UriKind.Absolute, out var fromUri))
                return importUrl;
            return new Uri(fromUri, importUrl).ToString();
        }

        int ReadUntilTerminator(out char terminator)
        {
            var paren = 0;
            var square = 0;
            while (!Eof)
            {
                if (StartsWith("/*"))
                {
                    ReadBlockComment();
                    continue;
                }

                if (StartsWith("//"))
                {
                    while (!Eof && Peek != '\n') Read();
                    continue;
                }

                var ch = Peek;
                if (ch is '"' or '\'')
                {
                    ReadString(ch);
                    continue;
                }

                if (ch == '(') paren++;
                else if (ch == ')') paren--;
                else if (ch == '[') square++;
                else if (ch == ']') square--;
                else if (paren == 0 && square == 0 && (ch == ';' || ch == '{' || ch == '}'))
                {
                    terminator = ch;
                    var end = _index;
                    if (ch == '{') Read();
                    return end;
                }

                Read();
            }

            terminator = '\0';
            return _index;
        }

        static int FindTopLevelColon(string text)
        {
            var paren = 0;
            var square = 0;
            for (var i = 0; i < text.Length; i++)
            {
                var ch = text[i];
                if (ch is '"' or '\'')
                {
                    i = SkipString(text, i);
                    continue;
                }

                if (ch == '(') paren++;
                else if (ch == ')') paren--;
                else if (ch == '[') square++;
                else if (ch == ']') square--;
                else if (ch == ':' && paren == 0 && square == 0) return i;
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

        static string? ExtractImportUrl(string parameters)
        {
            parameters = parameters.Trim();
            if (parameters.Length >= 2 && parameters[0] is '"' or '\'' && parameters[^1] == parameters[0])
                return parameters[1..^1];
            if (parameters.StartsWith("url(", StringComparison.OrdinalIgnoreCase) && parameters.EndsWith(")"))
            {
                var value = parameters[4..^1].Trim();
                if (value.Length >= 2 && value[0] is '"' or '\'' && value[^1] == value[0])
                    return value[1..^1];
                return value;
            }

            return null;
        }

        static string UnquoteScssString(string value)
        {
            value = value.Trim();
            if (value.Length >= 2 && value[0] is '"' or '\'' && value[^1] == value[0])
                return value[1..^1];
            return value;
        }

        void SkipIgnored()
        {
            while (!Eof)
            {
                if (char.IsWhiteSpace(Peek))
                {
                    Read();
                    continue;
                }

                if (StartsWith("/*"))
                {
                    ReadBlockComment();
                    continue;
                }

                if (StartsWith("//"))
                {
                    while (!Eof && Peek != '\n') Read();
                    continue;
                }

                return;
            }
        }

        void ReadBlockComment()
        {
            Read();
            Read();
            while (!Eof && !StartsWith("*/")) Read();
            if (!Eof)
            {
                Read();
                Read();
            }
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
        }

        void Expect(char ch)
        {
            SkipIgnored();
            if (Eof || Peek != ch) throw Error("Expected " + ch);
            Read();
        }

        bool StartsWith(string text) => _source.AsSpan(_index).StartsWith(text, StringComparison.Ordinal);

        char Read()
        {
            var ch = _source[_index++];
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

        Exception Error(string message) => new CssParseException(message, CurrentPosition());
        bool Eof => _index >= _source.Length;
        char Peek => _source[_index];
        Position CurrentPosition() => new(_line, _column, _index);

        static bool IsNameChar(char ch)
        {
            return char.IsLetterOrDigit(ch) || ch is '-' or '_';
        }
    }
}
