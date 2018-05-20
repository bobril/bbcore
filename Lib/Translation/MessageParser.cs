using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Lib.Translation
{
    class MessageParser
    {
        string _sourceText;
        int _pos;
        int _length;
        int _curLine;
        int _curCol;
        int _nextLine;
        int _nextCol;
        int _curToken;
        string _errorMsg;
        StringBuilder _sb = new StringBuilder();

        const int _eOFToken = -1;
        const int _errorToken = -2;
        const int _openBracketToken = -3;
        const int _closeBracketToken = -4;
        const int _hashToken = -5;

        void AdvanceNextToken()
        {
            _curLine = _nextLine;
            _curCol = _nextCol;
            if (_pos == _length)
            {
                _curToken = _eOFToken;
                return;
            }
            var ch = _sourceText[_pos++];
            if (ch == '\r' || ch == '\n')
            {
                _nextLine++; _nextCol = 1;
                if (ch == '\r' && _pos < _length && _sourceText[_pos] == '\n')
                {
                    _pos++;
                }
                _curToken = '\n';
                return;
            }
            _nextCol++;
            if (ch == '\\')
            {
                if (_pos == _length)
                {
                    _curToken = '\\';
                    return;
                }
                ch = _sourceText[_pos++];
                _nextCol++;
                if (ch == '\\' || ch == '{' || ch == '}' || ch == '#')
                {
                    _curToken = ch;
                    return;
                }
                if (ch == 'u')
                {
                    if (_pos + 4 <= _length)
                    {
                        if (uint.TryParse(_sourceText.AsSpan().Slice(_pos, 4), out var unicode))
                        {
                            _curToken = (int)unicode;
                            _pos += 4;
                            _nextCol += 4;
                            return;
                        }
                    }
                    _errorMsg = "After \\u there must be 4 hex characters";
                    _curToken = _errorToken;
                    return;
                }
                _errorMsg = "After \\ there could be only one of \\{}#u characters";
                _curToken = _errorToken;
                return;
            }
            if (ch == '{')
            {
                _curToken = _openBracketToken;
            }
            else if (ch == '}')
            {
                _curToken = _closeBracketToken;
            }
            else if (ch == '#')
            {
                _curToken = _hashToken;
            }
            else
            {
                _curToken = ch;
            }
        }

        bool IsError(object val)
        {
            return val is ParserException;
        }

        ParserException BuildError(string msg = null)
        {
            if (msg == null) msg = _errorMsg;
            return new ParserException(msg, _pos - 1, _curLine, _curCol);
        }

        void SkipWs()
        {
            while (_curToken == '\t' || _curToken == '\n' || _curToken == '\r' || _curToken == ' ')
            {
                AdvanceNextToken();
            }
        }

        object ParseIdentificator()
        {
            _sb.Clear();
            if (_curToken >= 'A' && _curToken <= 'Z' || _curToken >= 'a' && _curToken <= 'z' || _curToken == '_')
            {
                do
                {
                    _sb.Append((char)_curToken);
                    AdvanceNextToken();
                }
                while (_curToken >= 'A' && _curToken <= 'Z' || _curToken >= 'a' && _curToken <= 'z' || _curToken == '_' || _curToken >= '0' && _curToken <= '9');
            }
            else
            {
                return BuildError("Expecting identifier [a-zA-Z_]");
            }
            return _sb.ToString();
        }

        string ParseChars()
        {
            _sb.Clear();
            do
            {
                _sb.Append(char.ConvertFromUtf32(_curToken));
                AdvanceNextToken();
            } while (_curToken >= 0 && _curToken != '\t' && _curToken != '\n' && _curToken != '\r' && _curToken != ' ');
            return _sb.ToString();
        }

        int ParseNumber()
        {
            _sb.Clear();
            do
            {
                _sb.Append((char)_curToken);
                AdvanceNextToken();
            } while (_curToken >= '0' && _curToken <= '9');
            return int.Parse(_sb.ToString());
        }

        bool IsComma()
        {
            return _curToken == ',';
        }

        bool IsOpenBracketToken()
        {
            return _curToken == _openBracketToken;
        }

        bool IsCloseBracketToken()
        {
            return _curToken == _closeBracketToken;
        }

        static HashSet<string> _numClasses = new HashSet<string> { "zero", "one", "two", "few", "many", "other" };

        object ParseFormat()
        {
            SkipWs();
            if (_curToken == _errorToken) return BuildError();
            var identificator = ParseIdentificator();
            if (IsError(identificator)) return identificator;
            SkipWs();
            if (_curToken == _errorToken) return BuildError();
            if (IsCloseBracketToken())
            {
                AdvanceNextToken();
            }
            if (!IsComma())
            {
                return BuildError("Expecting \"}\" or \",\"");
            }
            AdvanceNextToken();
            SkipWs();
            var format = new Dictionary<string, object> { ["type"] = null };
            var res = new Dictionary<string, object>
            {
                ["type"] = "format",
                ["id"] = identificator,
                ["format"] = format
            };
            var name = ParseIdentificator();
            if (IsError(name)) return name;
            SkipWs();
            if (_curToken == _errorToken) return BuildError();
            if ((string)name == "number" || (string)name == "time" || (string)name == "date")
            {
                format["type"] = name;
                format["style"] = null;
                format["options"] = null;
                if (IsCloseBracketToken())
                {
                    AdvanceNextToken();
                    return res;
                }
                if (IsComma())
                {
                    AdvanceNextToken();
                    SkipWs();
                    var style = ParseIdentificator();
                    if (IsError(style)) return name;
                    format["style"] = style;
                    format["options"] = new List<KeyValuePair<object, object>>();
                    while (true)
                    {
                        SkipWs();
                        if (_curToken == _errorToken) return BuildError();
                        if (IsCloseBracketToken())
                        {
                            AdvanceNextToken();
                            return res;
                        }
                        if (IsComma())
                        {
                            AdvanceNextToken();
                            SkipWs();
                            var optionName = ParseIdentificator();
                            if (IsError(optionName)) return optionName;
                            if (_curToken == ':')
                            {
                                AdvanceNextToken();
                                SkipWs();
                                object val;
                                if (_curToken >= '0' && _curToken <= '9')
                                {
                                    val = ParseNumber();
                                }
                                else if (IsOpenBracketToken())
                                {
                                    AdvanceNextToken();
                                    val = ParseMsg(false);
                                }
                                else
                                {
                                    val = ParseIdentificator();
                                }
                                if (IsError(val)) return val;
                                ((List<KeyValuePair<object, object>>)format["options"]).Add(new KeyValuePair<object, object>(optionName, val));
                            }
                            else
                            {
                                ((List<KeyValuePair<object, object>>)format["options"]).Add(new KeyValuePair<object, object>(optionName, null));
                            }
                            continue;
                        }
                        break;
                    }
                }
                return BuildError("Expecting \",\" or \"}\"");
            }
            else if ((string)name == "plural" || (string)name == "selectordinal")
            {
                var options = new List<KeyValuePair<object, object>>();
                format["type"] = "plural";
                format["ordinal"] = (string)name != "plural";
                format["offset"] = 0;
                format["options"] = options;
                if (!IsComma())
                {
                    return BuildError("Expecting \",\"");
                }
                AdvanceNextToken();
                SkipWs();
                var offsetAllowed = true;
                while (!IsCloseBracketToken())
                {
                    if (_curToken < 0)
                    {
                        return BuildError("Expecting characters except \"{\", \"#\"");
                    }
                    var chars = ParseChars();
                    SkipWs();
                    if (offsetAllowed && chars.StartsWith("offset:"))
                    {
                        var m = new Regex("^offset: *([0-9]+)$").Match(chars);
                        if (m.Success)
                        {
                            format["offset"] = int.Parse(m.Groups[1].Value);
                        }
                        else if (chars == "offset:")
                        {
                            SkipWs();
                            if (_curToken < '0' || _curToken > '9')
                            {
                                return BuildError("Expecting number");
                            }
                            format["offset"] = ParseNumber();
                        }
                        else return BuildError("After \"offset:\" there must be number");
                        offsetAllowed = false;
                        continue;
                    }
                    offsetAllowed = false;
                    object selector;
                    if (new Regex("^=[0-9]+$").IsMatch(chars))
                    {
                        selector = int.Parse(chars.AsSpan().Slice(1));
                    }
                    else
                    {
                        selector = chars;
                        if (!_numClasses.Contains(selector)) return BuildError("Selector " + selector + " is not one of " + string.Join(", ", _numClasses.ToArray()));
                    }
                    if (!IsOpenBracketToken())
                    {
                        return BuildError("Expecting \"{\"");
                    }
                    AdvanceNextToken();
                    var value = ParseMsg(false);
                    if (IsError(value)) return value;
                    options.Add(new KeyValuePair<object, object>(selector, value));
                    SkipWs();
                }
                AdvanceNextToken();
                return res;
            }
            else if ((string)name == "select")
            {
                var options = new List<KeyValuePair<object, object>>();
                format["type"] = "select";
                format["options"] = options;
                if (!IsComma())
                {
                    return BuildError("Expecting \",\"");
                }
                AdvanceNextToken();
                SkipWs();
                while (!IsCloseBracketToken())
                {
                    if (_curToken < 0)
                    {
                        return BuildError("Expecting characters except \"{\", \"#\"");
                    }
                    var chars = ParseChars();
                    SkipWs();
                    object selector;
                    if (new Regex("^=[0-9]+$").IsMatch(chars))
                    {
                        selector = int.Parse(chars.AsSpan().Slice(1));
                    }
                    else
                    {
                        selector = chars;
                    }
                    if (!IsOpenBracketToken())
                    {
                        return BuildError("Expecting \"{\"");
                    }
                    AdvanceNextToken();
                    var value = ParseMsg(false);
                    if (IsError(value)) return value;
                    options.Add(new KeyValuePair<object, object>(selector, value));
                    SkipWs();
                }
                AdvanceNextToken();
                return res;
            }
            return BuildError("Expecting one of \"number\", \"time\", \"date\", \"plural\", \"selectordinal\", \"select\".");
        }

        object ParseMsg(bool endWithEOF)
        {
            object res = null;
            while (true)
            {
                if (_curToken == _errorToken)
                {
                    return BuildError();
                }
                if (_curToken == _eOFToken)
                {
                    if (endWithEOF)
                    {
                        if (res == null) return "";
                        return res;
                    }
                    return BuildError("Unexpected end of message missing \"}\"");
                }
                object val = null;
                if (_curToken == _openBracketToken)
                {
                    AdvanceNextToken();
                    val = ParseFormat();
                }
                else if (_curToken == _hashToken)
                {
                    AdvanceNextToken();
                    val = new Dictionary<string, object> { ["type"] = "hash" };
                }
                else if (_curToken == _closeBracketToken)
                {
                    if (endWithEOF)
                    {
                        return BuildError("Unexpected \"}\". Maybe you forgot to prefix it with \"\\\".");
                    }
                    AdvanceNextToken();
                    if (res == null) return "";
                    return res;
                }
                else
                {
                    _sb.Clear();
                    while (_curToken >= 0)
                    {
                        _sb.Append(char.ConvertFromUtf32(_curToken));
                        AdvanceNextToken();
                    }
                    val = _sb.ToString();
                }
                if (IsError(val)) return val;
                if (res == null) res = val;
                else
                {
                    if (res is List<object>)
                    {
                        ((List<object>)res).Add(val);
                    }
                    else
                    {
                        res = new List<object> { res, val };
                    }
                }
            }
        }

        public object Parse(string text)
        {
            _pos = 0;
            _sourceText = text;
            _length = text.Length;
            _nextLine = 1;
            _nextCol = 1;
            AdvanceNextToken();
            return ParseMsg(true);
        }
    }
}
