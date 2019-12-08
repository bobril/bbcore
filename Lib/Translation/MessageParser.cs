using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SixLabors.ImageSharp.PixelFormats;

namespace Lib.Translation
{
    public abstract class MessageAst
    {
        public virtual void GatherParams(HashSet<string> pars)
        {
        }
    }

    public class ErrorAst : MessageAst
    {
        public ErrorAst(string message, int position, int line, int column)
        {
            Message = message;
            Position = position;
            Line = line;
            Column = column;
        }

        public string Message { get; }
        public int Position { get; }
        public int Line { get; }
        public int Column { get; }

        public override string ToString()
        {
            return Message + (Position != 0 ? (" (" + Position + ")") : "");
        }
    }

    public class TextAst : MessageAst
    {
        public TextAst(string text)
        {
            Text = text;
        }

        public string Text { get; }
    }

    public class NumberAst : MessageAst
    {
        public NumberAst(int value)
        {
            Value = value;
        }

        public int Value { get; }
    }

    public class ListAst : MessageAst
    {
        public ListAst(MessageAst first, MessageAst second)
        {
            List = new List<MessageAst> {first, second};
        }

        public void Add(MessageAst item)
        {
            List.Add(item);
        }

        public List<MessageAst> List { get; }

        public override void GatherParams(HashSet<string> pars)
        {
            List.ForEach(i => i.GatherParams(pars));
        }
    }

    public class ConcatAst : MessageAst
    {
        public ConcatAst(ListAst from)
        {
            List = from.List;
        }

        public List<MessageAst> List { get; }

        public override void GatherParams(HashSet<string> pars)
        {
            List.ForEach(i => i.GatherParams(pars));
        }
    }

    public class HashAst : MessageAst
    {
    }

    public class ElAst : MessageAst
    {
        public int Id;
        public MessageAst? Value;

        public override void GatherParams(HashSet<string> pars)
        {
            pars.Add(Id.ToString());
            Value?.GatherParams(pars);
        }
    }

    public class StartElAst : MessageAst
    {
        public int Id;
    }

    public class CloseElAst : MessageAst
    {
        public int Id;
    }

    public class FormatAst : MessageAst
    {
        public FormatAst(string id)
        {
            Id = id;
        }

        public string Id { get; }
        public MessageAst Format { get; set; }

        public override void GatherParams(HashSet<string> pars)
        {
            pars.Add(Id);
            if (Format != null)
                Format.GatherParams(pars);
        }
    }

    public class FormatTypeAst : MessageAst
    {
        public FormatTypeAst(string type)
        {
            Type = type;
            Options = new List<KeyValuePair<string, MessageAst>>();
        }

        public string Type { get; }
        public string Style { get; set; }
        public List<KeyValuePair<string, MessageAst>> Options { get; }

        public override void GatherParams(HashSet<string> pars)
        {
            Options.ForEach(i => i.Value?.GatherParams(pars));
        }
    }

    public class PluralAst : MessageAst
    {
        public PluralAst(bool ordinal)
        {
            Ordinal = ordinal;
            Options = new List<KeyValuePair<object, MessageAst>>();
        }

        public bool Ordinal { get; }
        public int Offset { get; set; }
        public List<KeyValuePair<object, MessageAst>> Options { get; }

        public override void GatherParams(HashSet<string> pars)
        {
            Options.ForEach(i => i.Value.GatherParams(pars));
        }
    }

    public class SelectAst : MessageAst
    {
        public SelectAst()
        {
            Options = new List<KeyValuePair<object, MessageAst>>();
        }

        public List<KeyValuePair<object, MessageAst>> Options { get; }

        public override void GatherParams(HashSet<string> pars)
        {
            Options.ForEach(i => i.Value.GatherParams(pars));
        }
    }

    public class MessageParser
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
                _nextLine++;
                _nextCol = 1;
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
                            _curToken = (int) unicode;
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
            return val is ErrorAst;
        }

        ErrorAst BuildError(string? msg = null)
        {
            if (msg == null) msg = _errorMsg;
            return new ErrorAst(msg, _pos - 1, _curLine, _curCol);
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
                    _sb.Append((char) _curToken);
                    AdvanceNextToken();
                } while (_curToken >= 'A' && _curToken <= 'Z' || _curToken >= 'a' && _curToken <= 'z' ||
                         _curToken == '_' || _curToken >= '0' && _curToken <= '9');
            }
            else
            {
                return BuildError("Expecting identifier [a-zA-Z_]");
            }

            return _sb.ToString();
        }

        object ParseIdentOrEl()
        {
            var res = ParseIdentificator();
            if (!IsError(res)) return res;
            _sb.Clear();
            if (_curToken >= '0' && _curToken <= '9' || _curToken == '/')
            {
                do
                {
                    _sb.Append((char) _curToken);
                    AdvanceNextToken();
                } while (_curToken >= '0' && _curToken <= '9');

                if (_curToken == '/')
                {
                    _sb.Append((char) _curToken);
                    AdvanceNextToken();
                }
            }
            else
            {
                return BuildError("Expecting identifier [a-zA-Z_] or element [/0-9]");
            }

            var str = _sb.ToString();
            if (!uint.TryParse(str.Trim('/'), out var id))
            {
                return BuildError('"' + str + "\" is not valid element");
            }

            if (str.StartsWith('/'))
            {
                if (str.EndsWith('/'))
                {
                    return BuildError("Element id cannot start and end with slash: " + str);
                }

                return new CloseElAst {Id = (int) id};
            }

            if (str.EndsWith('/'))
            {
                return new ElAst {Id = (int) id};
            }

            return new StartElAst {Id = (int) id};
        }

        string ParseChars()
        {
            _sb.Clear();
            do
            {
                AppendCurTokenToSb();
                AdvanceNextToken();
            } while (_curToken >= 0 && _curToken != '\t' && _curToken != '\n' && _curToken != '\r' && _curToken != ' ');

            return _sb.ToString();
        }

        int ParseNumber()
        {
            _sb.Clear();
            do
            {
                _sb.Append((char) _curToken);
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

        static HashSet<string> _numClasses = new HashSet<string> {"zero", "one", "two", "few", "many", "other"};

        MessageAst ParseFormat()
        {
            SkipWs();
            if (_curToken == _errorToken) return BuildError();
            var ident = ParseIdentOrEl();
            if (IsError(ident)) return (ErrorAst) ident;
            SkipWs();
            if (_curToken == _errorToken) return BuildError();
            if (IsCloseBracketToken())
            {
                AdvanceNextToken();
                if (ident is MessageAst messageAst) return messageAst;
                return new FormatAst((string) ident);
            }

            if (ident is MessageAst)
            {
                return BuildError("Element cannot have parameters");
            }

            if (!IsComma())
            {
                return BuildError("Expecting \"}\" or \",\"");
            }

            AdvanceNextToken();
            SkipWs();
            var res = new FormatAst((string) ident);
            var name = ParseIdentificator();
            if (IsError(name)) return (ErrorAst) name;
            SkipWs();
            if (_curToken == _errorToken) return BuildError();
            if ((string) name == "number" || (string) name == "time" || (string) name == "date")
            {
                res.Format = new FormatTypeAst((string) name);
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
                    if (IsError(style)) return (ErrorAst) style;
                    ((FormatTypeAst) res.Format).Style = (string) style;
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
                            if (IsError(optionName)) return (ErrorAst) optionName;
                            if (_curToken == ':')
                            {
                                AdvanceNextToken();
                                SkipWs();
                                MessageAst val;
                                if (_curToken >= '0' && _curToken <= '9')
                                {
                                    val = ParseNumberAsAst();
                                }
                                else if (IsOpenBracketToken())
                                {
                                    AdvanceNextToken();
                                    val = ParseMsg(-1);
                                }
                                else
                                {
                                    val = ParseIdentificatorAsAst();
                                }

                                if (IsError(val)) return (ErrorAst) val;
                                ((FormatTypeAst) res.Format).Options.Add(
                                    new KeyValuePair<string, MessageAst>((string) optionName, val));
                            }
                            else
                            {
                                ((FormatTypeAst) res.Format).Options.Add(
                                    new KeyValuePair<string, MessageAst>((string) optionName, null));
                            }

                            continue;
                        }

                        break;
                    }
                }

                return BuildError("Expecting \",\" or \"}\"");
            }

            if ((string) name == "plural" || (string) name == "selectordinal")
            {
                res.Format = new PluralAst((string) name != "plural");
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
                            ((PluralAst) res.Format).Offset = int.Parse(m.Groups[1].Value);
                        }
                        else if (chars == "offset:")
                        {
                            SkipWs();
                            if (_curToken < '0' || _curToken > '9')
                            {
                                return BuildError("Expecting number");
                            }

                            ((PluralAst) res.Format).Offset = ParseNumber();
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
                        if (!_numClasses.Contains(selector))
                            return BuildError("Selector " + selector + " is not one of " +
                                              string.Join(", ", _numClasses.ToArray()));
                    }

                    if (!IsOpenBracketToken())
                    {
                        return BuildError("Expecting \"{\"");
                    }

                    AdvanceNextToken();
                    var value = ParseMsg(-1);
                    if (IsError(value)) return value;
                    ((PluralAst) res.Format).Options.Add(new KeyValuePair<object, MessageAst>(selector, value));
                    SkipWs();
                }

                AdvanceNextToken();
                return res;
            }

            if ((string) name == "select")
            {
                res.Format = new SelectAst();
                var options = ((SelectAst) res.Format).Options;
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
                    var value = ParseMsg(-1);
                    if (IsError(value)) return value;
                    options.Add(new KeyValuePair<object, MessageAst>(selector, value));
                    SkipWs();
                }

                AdvanceNextToken();
                return res;
            }

            return BuildError(
                "Expecting one of \"number\", \"time\", \"date\", \"plural\", \"selectordinal\", \"select\".");
        }

        MessageAst ParseNumberAsAst()
        {
            return new NumberAst(ParseNumber());
        }

        MessageAst ParseIdentificatorAsAst()
        {
            var res = ParseIdentificator();
            if (IsError(res)) return (ErrorAst) res;
            return new TextAst((string) res);
        }

        MessageAst ParseMsg(int endWithEOF)
        {
            MessageAst? res = null;
            var wrapByConcat = false;

            MessageAst Normalize(MessageAst? res)
            {
                if (res == null) return new TextAst("");
                if (wrapByConcat && (res is ListAst list)) return new ConcatAst(list);
                return res;
            }

            while (true)
            {
                if (_curToken == _errorToken)
                {
                    return BuildError();
                }

                if (_curToken == _eOFToken)
                {
                    if (endWithEOF == -2)
                    {
                        return Normalize(res);
                    }

                    if (endWithEOF >= 0)
                    {
                        return BuildError("Unexpected end of message. Missing end of element {/" + endWithEOF + "}");
                    }

                    return BuildError("Unexpected end of message. Missing \"}\"");
                }

                MessageAst val;
                if (_curToken == _openBracketToken)
                {
                    AdvanceNextToken();
                    val = ParseFormat();
                    if (val is StartElAst start)
                    {
                        var nested = ParseMsg(start.Id);
                        if (IsError(nested))
                            return nested;
                        wrapByConcat = true;
                        val = new ElAst {Id = start.Id, Value = nested};
                    }
                    else if (val is CloseElAst close)
                    {
                        if (close.Id == endWithEOF)
                        {
                            return Normalize(res);
                        }

                        return BuildError($"Missing {{/{endWithEOF}}}, got {{/{close.Id}}} instead.");
                    }
                    else if (val is ElAst)
                    {
                        wrapByConcat = true;
                    }
                }
                else if (_curToken == _hashToken)
                {
                    AdvanceNextToken();
                    val = new HashAst();
                }
                else if (_curToken == _closeBracketToken)
                {
                    if (endWithEOF != -1)
                    {
                        return BuildError("Unexpected \"}\". Maybe you forgot to prefix it with \"\\\".");
                    }

                    AdvanceNextToken();
                    return Normalize(res);
                }
                else
                {
                    _sb.Clear();
                    while (_curToken >= 0)
                    {
                        AppendCurTokenToSb();
                        AdvanceNextToken();
                    }

                    val = new TextAst(_sb.ToString());
                }

                if (IsError(val)) return val;
                if (res == null) res = val;
                else
                {
                    if (res is ListAst)
                    {
                        ((ListAst) res).Add(val);
                    }
                    else
                    {
                        res = new ListAst(res, val);
                    }
                }
            }
        }

        void AppendCurTokenToSb()
        {
            if (_curToken <= 0xffff)
                _sb.Append((char) _curToken);
            else
                _sb.Append(char.ConvertFromUtf32(_curToken));
        }

        public MessageAst Parse(string text)
        {
            _pos = 0;
            _sourceText = text;
            _length = text.Length;
            _nextLine = 1;
            _nextCol = 1;
            AdvanceNextToken();
            return ParseMsg(-2);
        }
    }
}
