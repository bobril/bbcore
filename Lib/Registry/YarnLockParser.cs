using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Lib.Registry;

public static class YarnLockParser
{
    static readonly Regex VersionRegex = new Regex("yarn lockfile v(\\d+)$");
    
    class Parser(string input)
    {
        SimpleYamlTokenizer.Token? _token;
        readonly IEnumerator<SimpleYamlTokenizer.Token> _tokens = SimpleYamlTokenizer.Tokenize(input).GetEnumerator();

        static void OnComment(SimpleYamlTokenizer.Token token)
        {
            var value = (string) token.Value!;

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
        }

        internal SimpleYamlTokenizer.Token Next()
        {
            while (true)
            {
                if (!_tokens.MoveNext())
                {
                    throw new EndOfStreamException("No more tokens");
                }

                var value = _tokens.Current;

                if (value.Type == SimpleYamlTokenizer.TokenTypes.Comment)
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
            throw new InvalidDataException($"{msg} {_token!.Line}:{_token.Col}");
        }

        internal Dictionary<string, object> Parse(int indent = 0)
        {
            var obj = new Dictionary<string, object>();

            while (true)
            {
                var propToken = _token!;

                if (propToken.Type == SimpleYamlTokenizer.TokenTypes.Newline)
                {
                    var nextToken = Next();
                    if (indent == 0)
                    {
                        // if we have 0 indentation then the next token doesn't matter
                        continue;
                    }

                    if (nextToken.Type != SimpleYamlTokenizer.TokenTypes.Indent)
                    {
                        // if we have no indentation after a newline then we've gone down a level
                        break;
                    }

                    if ((int) nextToken.Value! == indent)
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
                else if (propToken.Type == SimpleYamlTokenizer.TokenTypes.Indent)
                {
                    if ((int) propToken.Value! == indent)
                    {
                        Next();
                    }
                    else
                    {
                        break;
                    }
                }
                else if (propToken.Type == SimpleYamlTokenizer.TokenTypes.Eof)
                {
                    break;
                }
                else if (propToken.Type == SimpleYamlTokenizer.TokenTypes.String)
                {
                    // property key
                    var keys = new List<string> { (propToken.Value as string)! };
                    Next();

                    // support multiple keys
                    while (_token!.Type == SimpleYamlTokenizer.TokenTypes.Comma)
                    {
                        Next(); // skip comma

                        var keyToken = _token;
                        if (keyToken.Type != SimpleYamlTokenizer.TokenTypes.String)
                        {
                            Unexpected("Expected string");
                        }

                        keys.Add((keyToken.Value as string)!);
                        Next();
                    }

                    var valToken = _token;

                    if (valToken.Type == SimpleYamlTokenizer.TokenTypes.Colon)
                    {
                        // object
                        Next();

                        // parse object
                        var val = Parse(indent + 1);

                        foreach (var key in keys)
                        {
                            obj[key] = val;
                        }

                        if (indent != 0 && _token.Type != SimpleYamlTokenizer.TokenTypes.Indent)
                        {
                            break;
                        }
                    }
                    else if (SimpleYamlTokenizer.IsValidPropValueToken(valToken.Type))
                    {
                        // plain value
                        foreach (var key in keys)
                        {
                            obj[key] = valToken.Value!;
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
    
    public static Dictionary<string, object> Parse(string str)
    {
        if (SimpleYamlTokenizer.HasMergeConflicts(str))
        {
            return new Dictionary<string, object>();
        }

        var parser = new Parser(str);
        parser.Next();
        return parser.Parse();
    }
}
