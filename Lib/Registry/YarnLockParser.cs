using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Lib.Registry;

public class YarnLockParser
{
    class Token
    {
        public int Line;
        public int Col;
        public TokenTypes Type;
        public object Value;
    }

    static readonly Regex VersionRegex = new Regex("yarn lockfile v(\\d+)$");

    enum TokenTypes
    {
        Boolean,
        String,
        Eof,
        Colon,
        Newline,
        Comment,
        Indent,
        Invalid,
        Number,
        Comma,
    }

    static bool IsValidPropValueToken(TokenTypes token)
    {
        return token == TokenTypes.Boolean || token == TokenTypes.String || token == TokenTypes.Number;
    }

    static IEnumerable<Token> Tokenize(string input)
    {
        var lastNewline = false;
        var line = 1;
        var col = 0;

        Token BuildToken(TokenTypes type, object value = null)
        {
            return new Token {Line = line, Col = col, Type = type, Value = value};
        }

        while (input.Length > 0)
        {
            var chop = 0;

            if (input[0] == '\n' || input[0] == '\r')
            {
                chop++;
                // If this is a \r\n line, ignore both chars but only add one new line
                if (input.Length > 1 && input[1] == '\n')
                {
                    chop++;
                }

                line++;
                col = 0;
                yield return BuildToken(TokenTypes.Newline);
            }
            else if (input[0] == '#')
            {
                chop++;

                var val = "";
                while (chop < input.Length && input[chop] != '\n')
                {
                    val += input[chop];
                    chop++;
                }

                yield return BuildToken(TokenTypes.Comment, val);
            }
            else if (input[0] == ' ')
            {
                if (lastNewline)
                {
                    var indent = 0;
                    for (var i = 0; input[i] == ' '; i++)
                    {
                        indent++;
                    }

                    if (indent % 2 != 0)
                    {
                        throw new InvalidDataException("Invalid number of spaces");
                    }
                    else
                    {
                        chop = indent;
                        yield return BuildToken(TokenTypes.Indent, indent / 2);
                    }
                }
                else
                {
                    chop++;
                }
            }
            else if (input[0] == '"')
            {
                var val = "";

                for (var i = 0; i < input.Length; i++)
                {
                    var currentChar = input[i];
                    val += currentChar;

                    if (i <= 0 || currentChar != '"') continue;
                    var isEscaped = input[i - 1] == '\\' && input[i - 2] != '\\';
                    if (!isEscaped)
                    {
                        break;
                    }
                }

                chop = val.Length;

                string valS;
                try
                {
                    valS = JToken.Parse(val).Value<string>();
                }
                catch (Exception)
                {
                    Console.WriteLine();
                    throw;
                }

                if (valS != null)
                    yield return BuildToken(TokenTypes.String, valS);
                else
                    yield return BuildToken(TokenTypes.Invalid);
            }
            else if (input[0] >= '0' && input[0] <= '9')
            {
                var val = "";
                for (var i = 0; input[i] >= '0' && input[i] <= '9'; i++)
                {
                    val += input[i];
                }

                chop = val.Length;

                yield return BuildToken(TokenTypes.Number, int.Parse(val));
            }
            else if (input.StartsWith("true"))
            {
                yield return BuildToken(TokenTypes.Boolean, true);
                chop = 4;
            }
            else if (input.StartsWith("false"))
            {
                yield return BuildToken(TokenTypes.Boolean, false);
                chop = 5;
            }
            else if (input[0] == ':')
            {
                yield return BuildToken(TokenTypes.Colon);
                chop++;
            }
            else if (input[0] == ',')
            {
                yield return BuildToken(TokenTypes.Comma);
                chop++;
            }
            else if (new Regex("^[a-zA-Z\\/-]").IsMatch(input))
            {
                var name = "";
                foreach (var ch in input)
                {
                    if (ch == ':' || ch == ' ' || ch == '\n' || ch == '\r' || ch == ',')
                    {
                        break;
                    }
                    else
                    {
                        name += ch;
                    }
                }

                chop = name.Length;

                yield return BuildToken(TokenTypes.String, name);
            }
            else
            {
                yield return BuildToken(TokenTypes.Invalid);
            }

            if (chop == 0)
            {
                // will trigger infinite recursion
                yield return BuildToken(TokenTypes.Invalid);
            }

            col += chop;
            lastNewline = input[0] == '\n' || (input[0] == '\r' && input[1] == '\n');
            input = input.Substring(chop);
        }

        yield return BuildToken(TokenTypes.Eof);
    }

    class Parser
    {
        public Parser(string input)
        {
            _comments = new List<string>();
            _tokens = Tokenize(input).GetEnumerator();
        }

        Token _token;
        readonly IEnumerator<Token> _tokens;
        readonly List<string> _comments;

        void OnComment(Token token)
        {
            var value = (string) token.Value;

            var comment = value.Trim();

            var versionMatch = VersionRegex.Match(comment);
            if (versionMatch.Success)
            {
                var version = int.Parse(versionMatch.Groups[1].Value);
                if (version > 1)
                {
                    throw new InvalidDataException("Don't know how to parse yarn.lock with version " + version);
                }
            }

            _comments.Add(comment);
        }

        internal Token Next()
        {
            while (true)
            {
                if (!_tokens.MoveNext())
                {
                    throw new EndOfStreamException("No more tokens");
                }

                var value = _tokens.Current;

                if (value.Type == TokenTypes.Comment)
                {
                    OnComment(value);
                }
                else
                {
                    return _token = value;
                }
            }
        }

        void Unexpected(string msg = "Unexpected token")
        {
            throw new InvalidDataException($"{msg} {_token.Line}:{_token.Col}");
        }

        internal Dictionary<string, object> Parse(int indent = 0)
        {
            var obj = new Dictionary<string, object>();

            while (true)
            {
                var propToken = _token;

                if (propToken.Type == TokenTypes.Newline)
                {
                    var nextToken = Next();
                    if (indent == 0)
                    {
                        // if we have 0 indentation then the next token doesn't matter
                        continue;
                    }

                    if (nextToken.Type != TokenTypes.Indent)
                    {
                        // if we have no indentation after a newline then we've gone down a level
                        break;
                    }

                    if ((int) nextToken.Value == indent)
                    {
                        // all is good, the indent is on our level
                        Next();
                    }
                    else
                    {
                        // the indentation is less than our level
                        break;
                    }
                }
                else if (propToken.Type == TokenTypes.Indent)
                {
                    if ((int) propToken.Value == indent)
                    {
                        Next();
                    }
                    else
                    {
                        break;
                    }
                }
                else if (propToken.Type == TokenTypes.Eof)
                {
                    break;
                }
                else if (propToken.Type == TokenTypes.String)
                {
                    // property key
                    var keys = new List<string> {propToken.Value as string};
                    Next();

                    // support multiple keys
                    while (_token.Type == TokenTypes.Comma)
                    {
                        Next(); // skip comma

                        var keyToken = _token;
                        if (keyToken.Type != TokenTypes.String)
                        {
                            Unexpected("Expected string");
                        }

                        keys.Add(keyToken.Value as string);
                        Next();
                    }

                    var valToken = _token;

                    if (valToken.Type == TokenTypes.Colon)
                    {
                        // object
                        Next();

                        // parse object
                        var val = Parse(indent + 1);

                        foreach (var key in keys)
                        {
                            obj[key] = val;
                        }

                        if (indent != 0 && _token.Type != TokenTypes.Indent)
                        {
                            break;
                        }
                    }
                    else if (IsValidPropValueToken(valToken.Type))
                    {
                        // plain value
                        foreach (var key in keys)
                        {
                            obj[key] = valToken.Value;
                        }

                        Next();
                    }
                    else
                    {
                        Unexpected("Invalid value type");
                    }
                }
                else
                {
                    Unexpected($"Unknown token: {propToken}");
                }
            }

            return obj;
        }
    }

    const string MergeConflictEnd = ">>>>>>>";
    const string MergeConflictSep = "=======";
    const string MergeConflictStart = "<<<<<<<";

    static bool HasMergeConflicts(string str)
    {
        return str.Contains(MergeConflictStart) && str.Contains(MergeConflictSep) &&
               str.Contains(MergeConflictEnd);
    }

    public static Dictionary<string, object> Parse(string str)
    {
        if (HasMergeConflicts(str))
        {
            return new Dictionary<string, object>();
        }

        var parser = new Parser(str);
        parser.Next();
        return parser.Parse();
    }
}