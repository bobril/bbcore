using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using BTDB.Collections;
using Lib.DiskCache;
using Newtonsoft.Json.Linq;

namespace Lib.Registry;

public static class PnpmLockParser
{
    class Parser(string input): IDisposable
    {
        SimpleYamlTokenizer.Token? _token;
        readonly IEnumerator<SimpleYamlTokenizer.Token> _tokens = SimpleYamlTokenizer.Tokenize(input).GetEnumerator();

        public void Dispose()
        {
            _tokens.Dispose();    
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

                if (value.Type != SimpleYamlTokenizer.TokenTypes.Comment)
                {
                    return _token = value;
                }
            }
        }

        void Unexpected(string msg = "Unexpected token")
        {
            throw new InvalidDataException($"{msg} {_token!.Line}:{_token.Col}");
        }

        internal Dictionary<string, string> Parse()
        {
            var stack = new StructList<(string, int)>();
            var currentIndent = 0;
            var obj = new Dictionary<string, string>();

            while (true)
            {
                var propToken = Next();

                if (propToken.Type == SimpleYamlTokenizer.TokenTypes.Newline)
                {
                    currentIndent = 0;
                    continue;
                }
                if (propToken.Type == SimpleYamlTokenizer.TokenTypes.Indent)
                {
                    currentIndent = (int) propToken.Value!;
                    continue;
                }
                if (propToken.Type == SimpleYamlTokenizer.TokenTypes.Eof)
                {
                    break;
                }
                if (propToken.Type == SimpleYamlTokenizer.TokenTypes.String)
                {
                    var key = (string) propToken.Value!;
                    propToken = Next();
                    if (propToken.Type == SimpleYamlTokenizer.TokenTypes.Colon)
                    {
                        while (stack.Count > 0 && stack.Last.Item2 >= currentIndent)
                        {
                            stack.Pop();
                        }
                        stack.Add((key, currentIndent));
                        propToken = Next();
                        if (propToken.Type == SimpleYamlTokenizer.TokenTypes.String)
                        {
                            if (stack.Count == 5 && stack[0].Item1=="importers" && stack[4].Item1=="version")
                            {
                                obj[stack[3].Item1] = (string) propToken.Value!;
                            }
                        }
                        else if (propToken.Type is SimpleYamlTokenizer.TokenTypes.Boolean
                                 or SimpleYamlTokenizer.TokenTypes.Number)
                        {
                            continue;
                        }
                        else if (propToken.Type == SimpleYamlTokenizer.TokenTypes.Newline)
                        {
                            currentIndent = 0;
                            continue;
                        }
                        else if (propToken.Type == SimpleYamlTokenizer.TokenTypes.ObjectStart)
                        {
                            while (propToken.Type != SimpleYamlTokenizer.TokenTypes.ObjectEnd)
                            {
                                propToken = Next();
                            }
                        }
                        else
                        {
                            Unexpected($"Unknown token: {propToken}");
                        }
                    }
                }
                else if (propToken.Type == SimpleYamlTokenizer.TokenTypes.ObjectStart)
                {
                    while (propToken.Type != SimpleYamlTokenizer.TokenTypes.ObjectEnd)
                    {
                        propToken = Next();
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
    
    public static Dictionary<string, string> Parse(string str)
    {
        if (SimpleYamlTokenizer.HasMergeConflicts(str))
        {
            return new Dictionary<string, string>();
        }

        var parser = new Parser(str);
        return parser.Parse();
    }
}
