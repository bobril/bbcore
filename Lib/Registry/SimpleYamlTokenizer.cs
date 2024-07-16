using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Lib.Registry;

public static class SimpleYamlTokenizer
{
    const string MergeConflictEnd = ">>>>>>>";
    const string MergeConflictSep = "=======";
    const string MergeConflictStart = "<<<<<<<";

    public static bool HasMergeConflicts(string str)
    {
        return str.Contains(MergeConflictStart) && str.Contains(MergeConflictSep) &&
               str.Contains(MergeConflictEnd);
    }

    public class Token
    {
        public int Line;
        public int Col;
        public TokenTypes Type;
        public object? Value;
    }
    
    public enum TokenTypes
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
        ObjectStart,
        ObjectEnd,
    }

    public static bool IsValidPropValueToken(TokenTypes token)
    {
        return token is TokenTypes.Boolean or TokenTypes.String or TokenTypes.Number;
    }

    public static IEnumerable<Token> Tokenize(string input)
    {
        var lastNewline = false;
        var line = 1;
        var col = 0;

        Token BuildToken(TokenTypes type, object? value = null)
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
                if (input[0] == '\r' && input.Length > 1 && input[1] == '\n')
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

                var valS = JToken.Parse(val).Value<string>();

                if (valS != null)
                    yield return BuildToken(TokenTypes.String, valS);
                else
                    yield return BuildToken(TokenTypes.Invalid);
            }
            else if (input[0] == '\'')
            {
                var val = "";

                for (var i = 0; i < input.Length; i++)
                {
                    var currentChar = input[i];
                    val += currentChar;

                    if (i <= 0 || currentChar != '\'') continue;
                    var isEscaped = input[i - 1] == '\\' && input[i - 2] != '\\';
                    if (!isEscaped)
                    {
                        break;
                    }
                }

                chop = val.Length;

                var valS = JToken.Parse(val).Value<string>();

                if (valS != null)
                    yield return BuildToken(TokenTypes.String, valS);
                else
                    yield return BuildToken(TokenTypes.Invalid);
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
            else if (input[0] == '{')
            {
                yield return BuildToken(TokenTypes.ObjectStart);
                chop++;
            }
            else if (input[0] == '}')
            {
                yield return BuildToken(TokenTypes.ObjectEnd);
                chop++;
            }
            else if (new Regex("^[0-9a-zA-Z\\.@/-]").IsMatch(input))
            {
                var name = "";
                foreach (var ch in input)
                {
                    if (ch is ':' or ' ' or '\n' or '\r' or ',' or '}')
                    {
                        break;
                    }

                    name += ch;
                }

                chop = name.Length;

                if (name == "true")
                {
                    yield return BuildToken(TokenTypes.Boolean, true);
                }
                else if (name == "false")
                {
                    yield return BuildToken(TokenTypes.Boolean, false);
                }
                else if (int.TryParse(name, out var res))
                {
                    yield return BuildToken(TokenTypes.Number, res);
                }
                else
                {
                    yield return BuildToken(TokenTypes.String, name);
                }
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
}