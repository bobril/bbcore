using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Syntax.Inlines;
using Njsast.Reader;

namespace BobrilMdx
{
    public class AutolinkAndJsxInlineParser : InlineParser
    {
        public AutolinkAndJsxInlineParser()
        {
            OpeningCharacters = new[] {'<'};
        }

        public override bool Match(InlineProcessor processor, ref StringSlice slice)
        {
            var saved = slice;
            int line;
            int column;
            if (LinkHelper.TryParseAutolink(ref slice, out var link, out var isEmail))
            {
                processor.Inline = new AutolinkInline(link)
                {
                    IsEmail = isEmail,
                    Span = new(processor.GetSourcePosition(saved.Start, out line, out column), processor.GetSourcePosition(slice.Start - 1)),
                    Line = line,
                    Column = column
                };
            }
            else
            {
                slice = saved;
                if (!TryParseMdxTag(ref slice))
                {
                    return false;
                }

                processor.Inline = new HtmlInline(saved.Text.Substring(saved.Start, slice.Start - saved.Start))
                {
                    Span = new(processor.GetSourcePosition(saved.Start, out line, out column), processor.GetSourcePosition(slice.Start - 1)),
                    Line = line,
                    Column = column
                };
            }

            return true;
        }

        static bool TryParseMdxTag(ref StringSlice text, bool canBeClose = true)
        {
            var c = text.CurrentChar;
            if (c != '<')
            {
                return false;
            }
            c = text.NextChar();
            var wasClosing = false;
            if (c is '/' && canBeClose)
            {
                c = text.NextChar();
                wasClosing = true;
            }

            if (!TryParseJsIdent(ref text))
            {
                return false;
            }

            c = text.CurrentChar;
            if (c is ':')
            {
                if (!TryParseJsIdent(ref text))
                {
                    return false;
                }

                c = text.CurrentChar;
            }
            else
            {
                while (c is '.')
                {
                    if (!TryParseJsIdent(ref text))
                    {
                        return false;
                    }

                    c = text.CurrentChar;
                }
            }

            space: ;
            SkipWhiteSpace(ref text);
            c = text.CurrentChar;

            if (c is '{')
            {
                text.NextChar();
                SkipWhiteSpace(ref text);
                c = text.CurrentChar;
                for (var i = 0; i < 3; i++)
                {
                    if (c is not '.') return false;
                    c = text.NextChar();
                }

                if (!SkipToRightCurlyBracket(ref text)) return false;
                goto space;
            }
            if (!wasClosing && c is '/')
            {
                c = text.NextChar();
                if (c is '>')
                {
                    text.NextChar();
                    return true;
                }

                return false;
            }
            if (c is '>')
            {
                text.NextChar();
                return true;
            }

            if (TryParseJsIdent(ref text))
            {
                c = text.CurrentChar;
                if (c is ':')
                {
                    text.NextChar();
                    if (!TryParseJsIdent(ref text))
                        return false;
                    c = text.CurrentChar;
                }
                SkipWhiteSpace(ref text);
                c = text.CurrentChar;
                if (c is '=')
                {
                    text.NextChar();
                    SkipWhiteSpace(ref text);
                    c = text.CurrentChar;
                    text.NextChar();
                    switch (c)
                    {
                        case '{':
                            if (!SkipToRightCurlyBracket(ref text))
                                return false;
                            break;
                        case '\'':
                            if (!SkipSingleQuotedString(ref text))
                                return false;
                            break;
                        case '"':
                            if (!SkipDoubleQuotedString(ref text))
                                return false;
                            break;
                        default:
                            return false;
                    }
                }

                goto space;
            }

            return false;
        }

        static bool SkipToRightCurlyBracket(ref StringSlice text)
        {
            while (true)
            {
                var c = text.CurrentChar;
                text.NextChar();
                switch (c)
                {
                    case '\0':
                        return false;
                    case '}':
                        return true;
                    case '\'':
                        if (!SkipSingleQuotedString(ref text))
                            return false;
                        break;
                    case '"':
                        if (!SkipDoubleQuotedString(ref text))
                            return false;
                        break;
                    case '{':
                        if (!SkipToRightCurlyBracket(ref text))
                            return false;
                        break;
                    case '(':
                        if (!SkipToRightParenthesis(ref text))
                            return false;
                        break;
                    case '[':
                        if (!SkipToRightSquareBracket(ref text))
                            return false;
                        break;
                }
            }
        }

        static bool SkipToRightSquareBracket(ref StringSlice text)
        {
            while (true)
            {
                var c = text.CurrentChar;
                text.NextChar();
                switch (c)
                {
                    case '\0':
                        return false;
                    case ']':
                        return true;
                    case '\'':
                        if (!SkipSingleQuotedString(ref text))
                            return false;
                        break;
                    case '"':
                        if (!SkipDoubleQuotedString(ref text))
                            return false;
                        break;
                    case '{':
                        if (!SkipToRightCurlyBracket(ref text))
                            return false;
                        break;
                    case '(':
                        if (!SkipToRightParenthesis(ref text))
                            return false;
                        break;
                    case '[':
                        if (!SkipToRightSquareBracket(ref text))
                            return false;
                        break;
                }
            }
        }

        static bool SkipToRightParenthesis(ref StringSlice text)
        {
            while (true)
            {
                var c = text.CurrentChar;
                text.NextChar();
                switch (c)
                {
                    case '\0':
                        return false;
                    case ')':
                        return true;
                    case '\'':
                        if (!SkipSingleQuotedString(ref text))
                            return false;
                        break;
                    case '"':
                        if (!SkipDoubleQuotedString(ref text))
                            return false;
                        break;
                    case '{':
                        if (!SkipToRightCurlyBracket(ref text))
                            return false;
                        break;
                    case '(':
                        if (!SkipToRightParenthesis(ref text))
                            return false;
                        break;
                    case '[':
                        if (!SkipToRightSquareBracket(ref text))
                            return false;
                        break;
                }
            }
        }

        static bool SkipSingleQuotedString(ref StringSlice text)
        {
            while (true)
            {
                var c = text.CurrentChar;
                text.NextChar();
                switch (c)
                {
                    case '\0':
                        return false;
                    case '\'':
                        return true;
                    case '\\':
                        text.NextChar();
                        break;
                }
            }
        }

        static bool SkipDoubleQuotedString(ref StringSlice text)
        {
            while (true)
            {
                var c = text.CurrentChar;
                text.NextChar();
                switch (c)
                {
                    case '\0':
                        return false;
                    case '\"':
                        return true;
                    case '\\':
                        text.NextChar();
                        break;
                }
            }
        }

        static void SkipWhiteSpace(ref StringSlice text)
        {
            var c = text.CurrentChar;
            while (IsWhiteSpace(c))
            {
                c = text.NextChar();
            }
        }

        static bool IsWhiteSpace(char c)
        {
            return c is ' ' or (char) CharCode.NoBreakSpace or '\n' or '\r' or (char) CharCode.ParagraphSeparator or
                (char) CharCode.LineSeparator or > (char) CharCode.BackSpace and < (char) CharCode.ShiftOut;
        }

        static bool TryParseJsIdent(ref StringSlice text)
        {
            var c = text.CurrentChar;
            if (!Parser.IsIdentifierStart(c)) return false;
            while (true)
            {
                c = text.NextChar();
                if (!Parser.IsIdentifierChar(c)) return true;
            }
        }
    }
}
