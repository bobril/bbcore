using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Njsast.Reader
{
    public sealed partial class Parser : IEnumerable<Token>
    {
        // Move to the next token
        void Next()
        {
            _lastTokEnd = End;
            _lastTokStart = Start;
            NextToken();
        }

        Token GetToken()
        {
            Next();
            return new Token(Type, Value, new SourceLocation(Start, End, SourceFile));
        }

        public IEnumerator<Token> GetEnumerator()
        {
            Token token;
            do
            {
                token = GetToken();
                yield return token;
            } while (token.Type != TokenType.Eof);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        // Toggle strict mode. Re-reads the next number or string to please
        // pedantic tests (`"use strict"; 010;` should fail).
        TokContext CurContext()
        {
            return _context[_context.Count - 1];
        }

        // Read a single token, updating the parser object's token-related
        // properties.
        internal void NextToken()
        {
            var curContext = CurContext();
            if (curContext == null || curContext.PreserveSpace != true) SkipSpace();

            Start = CurPosition();
            if (_pos.Index >= _input.Length)
            {
                FinishToken(TokenType.Eof);
                return;
            }

            if (curContext?.Override != null) curContext.Override(this);
            else ReadToken(FullCharCodeAtPos());
        }

        void ReadToken(int code)
        {
            // Identifier or keyword. '\uXXXX' sequences are allowed in
            // identifiers, so '\' also dispatches to that.
            if (IsIdentifierStart(code, Options.EcmaVersion >= 6) || code == CharCode.BackSlash /* '\' */)
            {
                ReadWord();
                return;
            }

            GetTokenFromCode(code);
        }

        int FullCharCodeAtPos()
        {
            if (_pos.Index >= _input.Length)
                return 0;

            return char.ConvertToUtf32(_input, _pos.Index);
        }

        void SkipBlockComment()
        {
            var startLocation = _pos;
            _pos = _pos.Increment(2);
            var end = _input.IndexOf("*/", _pos.Index, StringComparison.Ordinal);
            if (end == -1) Raise(_pos.Increment(-2), "Unterminated comment");
            _pos = new Position(_pos.Line, _pos.Column + (end - startLocation.Index), end + 2);
            var lastIndex = startLocation.Index;
            while (true)
            {
                var match = LineBreak.Match(_input, lastIndex);
                if (!match.Success || match.Index >= _pos.Index)
                    break;

                var lineStart = match.Index + match.Length;
                _pos = new Position(_pos.Line + 1, _pos.Index - lineStart, _pos.Index);
                lastIndex = lineStart;
            }
            Options.OnComment?.Invoke(true, _input.Substring(startLocation.Index + 2, end - (startLocation.Index + 2)), new SourceLocation(startLocation, _pos, SourceFile));
        }

        void SkipLineComment(int startSkip)
        {
            var start = _pos;
            _pos = _pos.Increment(startSkip);
            var ch = _input.Get(_pos.Index);
            while (_pos.Index < _input.Length && !IsNewLine(ch))
            {
                _pos = _pos.Increment(1);
                ch = _input.Get(_pos.Index);
            }
            Options.OnComment?.Invoke(false, _input.Substring(start.Index + startSkip, _pos.Index - (start.Index + startSkip)), new SourceLocation(start, _pos, SourceFile));
        }

        // Called at the start of the parse and after every token. Skips
        // whitespace and comments, and.
        void SkipSpace()
        {
            while (_pos.Index < _input.Length)
            {
                var ch = (int)_input[_pos.Index];
                switch (ch)
                {
                    case CharCode.Space:
                    case CharCode.NoBreakSpace:
                        _pos = _pos.Increment(1);
                        break;
                    case CharCode.CarriageReturn:
                        if (_pos.Index + 1 < _input.Length && _input[_pos.Index + 1] == 10)
                            _pos = _pos.Increment(1);
                        goto case CharCode.LineFeed;
                    case CharCode.LineFeed:
                    case CharCode.LineSeparator:
                    case CharCode.ParagraphSeparator:
                        _pos = new Position(_pos.Line + 1, 0, _pos.Index + 1);
                        break;
                    case CharCode.Slash:
                        switch ((int)_input.Get(_pos.Index + 1))
                        {
                            case CharCode.Asterisk:
                                SkipBlockComment();
                                break;
                            case CharCode.Slash:
                                SkipLineComment(2);
                                break;
                            default:
                                return;
                        }
                        break;
                    default:
                        if (ch > CharCode.BackSpace && ch < CharCode.ShiftOut || ch >= CharCode.OghamSpaceMark && NonAsciIwhitespace.IsMatch(((char)ch).ToString()))
                        {
                            _pos = _pos.Increment(1);
                            break;
                        }
                        else
                        {
                            return;
                        }
                }
            }
        }

        // Called at the end of every token. Sets `end`, `val`, and
        // maintains `context` and `exprAllowed`, and skips the space after
        // the token, so that the next one's `start` will point at the
        // right position.
        void FinishToken(TokenType type, object? val = null)
        {
            End = CurPosition();
            var prevType = Type;
            Type = type;
            Value = val;

            UpdateContext(prevType);
        }

        // ### Token reading

        // This is the function that is called to fetch the next token. It
        // is somewhat obscure, because it works in character codes rather
        // than characters, and because operator parsing has been inlined
        // into it.
        //
        // All in the name of speed.
        //
        void readToken_dot()
        {
            var next = _input[_pos.Index + 1];
            if (next >= CharCode.DigitZero && next <= CharCode.DigitNine)
            {
                ReadNumber(true);
                return;
            }
            var next2 = _input[_pos.Index + 2];
            if (Options.EcmaVersion >= 6 && next == CharCode.Dot && next2 == CharCode.Dot)
            {
                _pos = _pos.Increment(3);
                FinishToken(TokenType.Ellipsis);
            }
            else
            {
                _pos = _pos.Increment(1);
                FinishToken(TokenType.Dot);
            }
        }

        void readToken_slash()
        { // '/'
            var next = _input.Get(_pos.Index + 1);
            if (_exprAllowed)
            {
                _pos = _pos.Increment(1);
                ReadRegexp();
                return;
            }
            if (next == 61) FinishOp(TokenType.Assign, 2);
            else FinishOp(TokenType.Slash, 1);
        }

        void readToken_mult_modulo_exp(int code)
        {
            // '%*'
            var next = _input.Get(_pos.Index + 1);
            var size = 1;
            var tokentype = code == CharCode.Asterisk ? TokenType.Star : TokenType.Modulo;

            // exponentiation operator ** and **=
            if (Options.EcmaVersion >= 7 && code == CharCode.Asterisk && next == CharCode.Asterisk)
            {
                ++size;
                tokentype = TokenType.Starstar;
                next = _input[_pos.Index + 2];
            }

            if (next == 61) FinishOp(TokenType.Assign, size + 1);
            else FinishOp(tokentype, size);
        }

        void readToken_pipe_amp(int code)
        {
            // '|&'
            var next = _input[_pos.Index + 1];
            if (next == code) FinishOp(code == 124 ? TokenType.LogicalOr : TokenType.LogicalAnd, 2);
            else if (next == 61) FinishOp(TokenType.Assign, 2);
            else FinishOp(code == 124 ? TokenType.BitwiseOr : TokenType.BitwiseAnd, 1);
        }

        void readToken_caret()
        {
            // '^'
            var next = _input[_pos.Index + 1];
            if (next == 61) FinishOp(TokenType.Assign, 2);
            else FinishOp(TokenType.BitwiseXor, 1);
        }

        void readToken_plus_min(int code)
        {
            // '+-'
            var next = _input[_pos.Index + 1];
            if (next == code)
            {
                if (next == 45 && !_inModule && _input.Get(_pos.Index + 2) == 62 &&
                    (_lastTokEnd.Index == 0 || LineBreak.IsMatch(_input.Substring(_lastTokEnd.Index, _pos - _lastTokEnd))))
                {
                    // A `-->` line comment
                    SkipLineComment(3);
                    SkipSpace();
                    NextToken();
                    return;
                }
                FinishOp(TokenType.IncDec, 2);
            }
            else if (next == 61) FinishOp(TokenType.Assign, 2);
            else FinishOp(TokenType.PlusMin, 1);
        }

        void readToken_lt_gt(int code)
        {
            // '<>'
            var next = _input[_pos.Index + 1];
            var size = 1;
            if (next == code)
            {
                size = code == 62 && _input[_pos.Index + 2] == 62 ? 3 : 2;
                if (_input[_pos.Index + size] == 61) FinishOp(TokenType.Assign, size + 1);
                else FinishOp(TokenType.BitShift, size);
                return;
            }
            if (next == 33 && code == 60 && !_inModule && _input[_pos.Index + 2] == 45 &&
                _input[_pos.Index + 3] == 45)
            {
                // `<!--`, an XML-style comment that should be interpreted as a line comment
                SkipLineComment(4);
                SkipSpace();
                NextToken();
                return;
            }
            if (next == 61) size = 2;
            FinishOp(TokenType.Relational, size);
        }

        void readToken_eq_excl(int code)
        {
            // '=!'
            var next = _input[_pos.Index + 1];
            if (next == 61) FinishOp(TokenType.Equality, _input[_pos.Index + 2] == 61 ? 3 : 2);
            else if (code == 61 && next == 62 && Options.EcmaVersion >= 6)
            {
                // '=>'
                _pos = _pos.Increment(2);
                FinishToken(TokenType.Arrow);
            }
            else FinishOp(code == 61 ? TokenType.Eq : TokenType.Prefix, 1);
        }

        void GetTokenFromCode(int code)
        {
            switch (code)
            {
                // The interpretation of a dot depends on whether it is followed
                // by a digit or another two dots.
                case 46: // '.'
                    readToken_dot();
                    return;

                // Punctuation tokens.
                case 40:
                    _pos = _pos.Increment(1);
                    FinishToken(TokenType.ParenL);
                    return;
                case 41:
                    _pos = _pos.Increment(1);
                    FinishToken(TokenType.ParenR);
                    return;
                case 59:
                    _pos = _pos.Increment(1);
                    FinishToken(TokenType.Semi);
                    return;
                case 44:
                    _pos = _pos.Increment(1);
                    FinishToken(TokenType.Comma);
                    return;
                case 91:
                    _pos = _pos.Increment(1);
                    FinishToken(TokenType.BracketL);
                    return;
                case 93:
                    _pos = _pos.Increment(1);
                    FinishToken(TokenType.BracketR);
                    return;
                case 123:
                    _pos = _pos.Increment(1);
                    FinishToken(TokenType.BraceL);
                    return;
                case 125:
                    _pos = _pos.Increment(1);
                    FinishToken(TokenType.BraceR);
                    return;
                case 58:
                    _pos = _pos.Increment(1);
                    FinishToken(TokenType.Colon);
                    return;
                case 63:
                    _pos = _pos.Increment(1);
                    FinishToken(TokenType.Question);
                    return;

                case 96: // '`'
                    if (Options.EcmaVersion < 6) break;
                    _pos = _pos.Increment(1);
                    FinishToken(TokenType.BackQuote);
                    return;

                case 48: // '0'
                    var next = _input.Get(_pos.Index + 1);
                    if (next == 120 || next == 88)
                    {
                        ReadRadixNumber(16); // '0x', '0X' - hex number
                        return;
                    }
                    if (Options.EcmaVersion >= 6)
                    {
                        if (next == 111 || next == 79)
                        {
                            ReadRadixNumber(8); // '0o', '0O' - octal number
                            return;
                        }
                        if (next == 98 || next == 66)
                        {
                            ReadRadixNumber(2); // '0b', '0B' - binary number
                            return;
                        }
                    }
                    goto case 49;
                // Anything else beginning with a digit is an integer, octal
                // number, or float.
                case 49:
                case 50:
                case 51:
                case 52:
                case 53:
                case 54:
                case 55:
                case 56:
                case 57: // 1-9
                    ReadNumber(false);
                    return;

                // Quotes produce strings.
                case 34:
                case 39: // '"', "'"
                    ReadString(code);
                    return;

                // Operators are parsed inline in tiny state machines. '=' (61) is
                // often referred to. `finishOp` simply skips the amount of
                // characters it is given as second argument, and returns a token
                // of the type given by its first argument.

                case 47: // '/'
                    readToken_slash();
                    return;

                case 37:
                case 42: // '%*'
                    readToken_mult_modulo_exp(code);
                    return;

                case 124:
                case 38: // '|&'
                    readToken_pipe_amp(code);
                    return;

                case 94: // '^'
                    readToken_caret();
                    return;

                case 43:
                case 45: // '+-'
                    readToken_plus_min(code);
                    return;

                case 60:
                case 62: // '<>'
                    readToken_lt_gt(code);
                    return;

                case 61:
                case 33: // '=!'
                    readToken_eq_excl(code);
                    return;

                case 126: // '~'
                    FinishOp(TokenType.Prefix, 1);
                    return;
            }

            Raise(_pos, "Unexpected character '" + CodePointToString(code) + "'");
        }

        void FinishOp(TokenType type, int size)
        {
            var str = _input.Substring(_pos.Index, size);
            _pos = _pos.Increment(size);
            FinishToken(type, str);
        }

        void ReadRegexp()
        {
            var escaped = false;
            var inClass = false;
            var start = _pos;
            while (true)
            {
                if (_pos.Index >= _input.Length) Raise(start, "Unterminated regular expression");
                var ch = _input[_pos.Index];
                if (LineBreak.IsMatch(ch.ToString())) Raise(start, "Unterminated regular expression");
                if (!escaped)
                {
                    if (ch == '[') inClass = true;
                    else if (ch == ']' && inClass) inClass = false;
                    else if (ch == '/' && !inClass) break;
                    escaped = ch == '\\';
                }
                else escaped = false;
                _pos = _pos.Increment(1);
            }

            var content = _input.Substring(start.Index, _pos - start);
            _pos = _pos.Increment(1);
            // Need to use `readWord1` because '\uXXXX' sequences are allowed
            // here (don't ask).
            var mods = ReadWord1();
            if (!string.IsNullOrEmpty(mods))
            {
                var validFlags = new Regex("^[gim]*$");
                if (Options.EcmaVersion >= 6) validFlags = new Regex("^[gimuy]*$");
                if (!validFlags.IsMatch(mods)) Raise(start, "Invalid regular expression flag");
            }
            FinishToken(TokenType.Regexp, new RegExp
            {
                Pattern = content,
                Flags = RegExp.String2Flags(mods)
            });
        }

        // Read an integer in the given radix. Return null if zero digits
        // were read, the integer value otherwise. When `len` is given, this
        // will return `null` unless the integer has exactly `len` digits.
        long? ReadInt(int radix, int? len = null)
        {
            var total = 0L;
            var start = _pos;
            for (var i = 0; !len.HasValue || i < len; ++i)
            {
                var code = _input.Get(_pos.Index);
                int val;
                if (code >= 97) val = code - 97 + 10; // a
                else if (code >= 65) val = code - 65 + 10; // A
                else if (code >= 48 && code <= 57) val = code - 48; // 0-9
                else val = int.MaxValue;
                if (val >= radix) break;
                _pos = _pos.Increment(1);
                total = total * radix + val;
            }

            if (_pos == start || len != null && _pos - start != len) return null;
            return total;
        }

        void ReadRadixNumber(int radix)
        {
            _pos = _pos.Increment(2); // 0x
            var val = ReadInt(radix);
            if (!val.HasValue)
            {
                throw NewSyntaxError(Start.Increment(2), "Expected number in radix " + radix);
            }
            if (IsIdentifierStart(FullCharCodeAtPos())) Raise(_pos, "Identifier directly after number");
            FinishToken(TokenType.Num, val.Value);
        }

        static readonly Regex Test89 = new Regex("[89]");

        // Read an integer, octal integer, or floating-point number.
        void ReadNumber(bool startsWithDot)
        {
            var start = _pos;
            if (!startsWithDot && ReadInt(10) == null) Raise(start, "Invalid number");
            var octal = _pos - start >= 2 && _input.Get(start.Index) == 48;
            if (octal && _strict) Raise(start, "Invalid number");
            if (octal && Test89.IsMatch(_input.Substring(start.Index, _pos - start))) octal = false;
            var next = _input.Get(_pos.Index);

            if (next == 46 && !octal)
            {
                // '.'
                _pos = _pos.Increment(1);
                ReadInt(10);
                next = _input.Get(_pos.Index);
            }

            if ((next == 69 || next == 101) && !octal)
            {
                // 'eE'
                _pos = _pos.Increment(1);
                next = _input.Get(_pos.Index);
                if (next == 43 || next == 45) _pos = _pos.Increment(1);// '+-'
                if (ReadInt(10) == null) Raise(start, "Invalid number");
            }
            if (IsIdentifierStart(FullCharCodeAtPos())) Raise(_pos, "Identifier directly after number");

            var str = _input.Substring(start.Index, _pos - start);
            var val = octal ? ParseInt(str, 8) : double.Parse(str, NumberStyles.Any, CultureInfo.InvariantCulture);
            FinishToken(TokenType.Num, val);
        }

        // Read a string value, interpreting backslash-escapes.
        int ReadCodePoint()
        {
            var ch = _input.Get(_pos.Index);
            int code;

            if (ch == 123)
            {
                // '{'
                if (Options.EcmaVersion < 6)
                {
                    Raise(Start, "Unexpected token");
                }
                var codePos = _pos = _pos.Increment(1);
                code = ReadHexChar(_input.IndexOf("}", _pos.Index, StringComparison.Ordinal) - _pos.Index);
                _pos = _pos.Increment(1);
                if (code > 0x10FFFF) InvalidStringToken(codePos, "Code point out of bounds");
            }
            else
            {
                code = ReadHexChar(4);
            }
            return code;
        }

        static string CodePointToString(int code)
        {
            return char.ConvertFromUtf32(code);
        }

        void ReadString(int quote)
        {
            var @out = "";
            var chunkStart = _pos = _pos.Increment(1);
            while (true)
            {
                if (_pos.Index >= _input.Length) Raise(Start, "Unterminated string constant");
                var ch = _input[_pos.Index];
                if (ch == quote) break;
                if (ch == 92)
                {
                    // '\'
                    @out += _input.Substring(chunkStart.Index, _pos - chunkStart);
                    @out += ReadEscapedChar(false);
                    chunkStart = _pos;
                }
                else
                {
                    if (IsNewLine(ch)) Raise(Start, "Unterminated string constant");
                    _pos = _pos.Increment(1);
                }
            }
            @out += _input.Substring(chunkStart.Index, _pos - chunkStart);
            _pos = _pos.Increment(1);
            FinishToken(TokenType.String, @out);
        }

        // Reads template string tokens.
        sealed class InvalidTemplateEscapeError : Exception
        {
        }

        public void TryReadTemplateToken()
        {
            _inTemplateElement = true;
            try
            {
                ReadTmplToken();
            }
            catch (InvalidTemplateEscapeError)
            {
                ReadInvalidTemplateToken();
            }

            _inTemplateElement = false;
        }

        void InvalidStringToken(Position position, string message)
        {
            if (_inTemplateElement && Options.EcmaVersion >= 9)
            {
                throw new InvalidTemplateEscapeError();
            }

            Raise(position, message);
        }

        void ReadTmplToken()
        {
            var @out = "";
            var chunkStart = _pos;
            while (true)
            {
                if (_pos.Index >= _input.Length) Raise(Start, "Unterminated template");
                var ch = _input[_pos.Index];
                if (ch == 96 || ch == 36 && _input[_pos.Index + 1] == 123)
                {
                    // '`', '${'
                    if (_pos.Index == Start.Index && (Type == TokenType.Template || Type == TokenType.InvalidTemplate))
                    {
                        if (ch == 36)
                        {
                            _pos = _pos.Increment(2);
                            FinishToken(TokenType.DollarBraceL);
                            return;
                        }

                        _pos = _pos.Increment(1);
                        FinishToken(TokenType.BackQuote);
                        return;
                    }
                    @out += _input.Substring(chunkStart.Index, _pos - chunkStart);
                    FinishToken(TokenType.Template, @out);
                    return;
                }
                if (ch == 92)
                {
                    // '\'
                    @out += _input.Substring(chunkStart.Index, _pos - chunkStart);
                    @out += ReadEscapedChar(true);
                    chunkStart = _pos;
                }
                else if (IsNewLine(ch))
                {
                    @out += _input.Substring(chunkStart.Index, _pos - chunkStart);
                    _pos = _pos.Increment(1);
                    switch ((int)ch)
                    {
                        case 13:
                            if (_input[_pos.Index] == 10)
                                _pos = _pos.Increment(1);
                            goto case 10;
                        case 10:
                            @out += "\n";
                            break;
                        default:
                            @out += ch.ToString();
                            break;
                    }
                    _pos = new Position(_pos.Line + 1, 0, _pos.Index);
                    chunkStart = _pos;
                }
                else
                {
                    _pos = _pos.Increment(1);
                }
            }
        }
        // Reads a template token to search for the end, without validating any escape sequences
        void ReadInvalidTemplateToken()
        {
            for (; _pos.Index < _input.Length; _pos = _pos.Increment(1))
            {
                switch (_input[_pos.Index])
                {
                    case '\\':
                        _pos = _pos.Increment(1);
                        break;

                    case '$':
                        if (_input[_pos.Index + 1] != '{')
                        {
                            break;
                        }
                        goto case '`';
                    // falls through

                    case '`':
                        FinishToken(TokenType.InvalidTemplate, _input.Substring(Start.Index, _pos - Start));
                        return;

                        // no default
                }
            }
            Raise(Start, "Unterminated template");
        }

        // Used to read escaped characters
        string ReadEscapedChar(bool inTemplate)
        {
            _pos = _pos.Increment(1);
            var ch = _input.Get(_pos.Index);
            _pos = _pos.Increment(1);
            switch ((int)ch)
            {
                case 110: return "\n"; // 'n' -> '\n'
                case 114: return "\r"; // 'r' -> '\r'
                case 120: return ((char)ReadHexChar(2)).ToString(); // 'x'
                case 117: return CodePointToString(ReadCodePoint()); // 'u'
                case 116: return "\t"; // 't' -> '\t'
                case 98: return "\b"; // 'b' -> '\b'
                case 118: return "\u000b"; // 'v' -> '\u000b'
                case 102: return "\f"; // 'f' -> '\f'
                case 13:
                    if (_input[_pos.Index] == 10)
                        _pos = _pos.Increment(1); // '\r\n'
                    goto case 10;
                case 10: // ' \n'
                    _pos = new Position(_pos.Line + 1, 0, _pos.Index);
                    return "";
                default:
                    if (ch >= 48 && ch <= 55)
                    {
                        var octalStr = new Regex("^[0-7]+").Match(_input.Substring(_pos.Index - 1, Math.Min(3, _input.Length - _pos.Index + 1))).Groups[0].Value;
                        var octal = ParseInt(octalStr, 8);
                        if (octal > 255)
                        {
                            octalStr = octalStr.Substring(0, octalStr.Length - 1);
                            octal = ParseInt(octalStr, 8);
                        }
                        if (octalStr != "0" && (_strict || inTemplate))
                        {
                            InvalidStringToken(_pos.Increment(-2), "Octal literal in strict mode");
                        }
                        _pos = _pos.Increment(octalStr.Length - 1);
                        return ((char)octal).ToString();
                    }
                    return ch.ToString();
            }
        }

        static int ParseInt(string str, int @base)
        {
            if (@base > 10)
                throw new NotImplementedException();

            const string numbers = "0123456789";
            var number = 0;
            foreach (var c in str)
            {
                number *= @base;
                var index = numbers.IndexOf(c, 0, @base);
                if (index < 0)
                    throw new NotImplementedException();
                number += index;
            }

            return number;
        }

        // Used to read character escape sequences ('\x', '\u', '\U').
        int ReadHexChar(int len)
        {
            var codePos = _pos;
            var n = ReadInt(16, len);
            if (!n.HasValue)
            {
                InvalidStringToken(codePos, "Bad character escape sequence");
                return 0;
            }
            return (int)n.Value;
        }

        // Read an identifier, and return it as a string. Sets `this.containsEsc`
        // to whether the word contained a '\u' escape.
        //
        // Incrementally adds only escaped chars, adding other chunks as-is
        // as a micro-optimization.
        string ReadWord1()
        {
            _containsEsc = false;
            var word = "";
            var first = true;
            var chunkStart = _pos;
            var astral = Options.EcmaVersion >= 6;
            while (_pos.Index < _input.Length)
            {
                var ch = FullCharCodeAtPos();
                if (IsIdentifierChar(ch, astral))
                {
                    _pos = _pos.Increment(ch <= 0xffff ? 1 : 2);
                }
                else if (ch == 92)
                {
                    // "\"
                    _containsEsc = true;
                    word += _input.Substring(chunkStart.Index, _pos - chunkStart);
                    var escStart = _pos;
                    _pos = _pos.Increment(1);
                    if (_input.Get(_pos.Index) != 117) // "u"
                        InvalidStringToken(_pos, "Expecting Unicode escape sequence \\uXXXX");
                    _pos = _pos.Increment(1);
                    var esc = ReadCodePoint();
                    if (!(first ? (Func<int, bool, bool>)IsIdentifierStart : IsIdentifierChar)(esc, astral))
                        InvalidStringToken(escStart, "Invalid Unicode escape");
                    word += CodePointToString(esc);
                    chunkStart = _pos;
                }
                else
                {
                    break;
                }
                first = false;
            }
            return word + _input.Substring(chunkStart.Index, _pos - chunkStart);
        }

        // Read an identifier or keyword token. Will check for reserved
        // words when necessary.
        void ReadWord()
        {
            var word = ReadWord1();
            var type = TokenType.Name;
            if (_keywords.IsMatch(word))
            {
                if (_containsEsc) RaiseRecoverable(Start, "Escape sequence in keyword " + word);
                type = TokenInformation.Keywords[word];
            }
            FinishToken(type, word);
        }
    }
}
