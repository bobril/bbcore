using Markdig.Helpers;
using Markdig.Parsers;

namespace BobrilMdx
{
    public class MdxCodeInlineParser : InlineParser
    {
        public MdxCodeInlineParser()
        {
            OpeningCharacters = new[] { '{' };
        }

        static int CountAndSkipChar(ref StringSlice slice, char matchChar)
        {
            var text = slice.Text;
            var end = slice.End;
            var current = slice.Start;

            while (current <= end && (uint)current < (uint)text.Length && text[current] == matchChar)
            {
                current++;
            }

            var count = current - slice.Start;
            slice.Start = current;
            return count;
        }

        public override bool Match(InlineProcessor processor, ref StringSlice slice)
        {
            var match = slice.CurrentChar;
            if (slice.PeekCharExtra(-1) == match)
            {
                return false;
            }

            var startPosition = slice.Start;

            var openSticks = CountAndSkipChar(ref slice, match);
            var closeSticks = 0;

            var c = slice.CurrentChar;

            var builder = StringBuilderCache.Local();

            // A { } string is a string of one or more `{` characters that is neither preceded nor followed by `{`.
            // A code span begins with a `{` string and ends with a `}` string of equal length.

            var contentEnd = -1;

            while (c != '\0')
            {
                if (c == '}')
                {
                    contentEnd = slice.Start;
                    closeSticks = CountAndSkipChar(ref slice, '}');

                    if (openSticks == closeSticks)
                    {
                        break;
                    }

                    builder.Append('}', closeSticks);
                    c = slice.CurrentChar;
                }
                else
                {
                    builder.Append(c);
                    c = slice.NextChar();
                }
            }

            if (closeSticks != openSticks) return false;
            var content = builder.ToString();
            var spanStart = processor.GetSourcePosition(startPosition, out var line, out var column);
            var spanEnd = processor.GetSourcePosition(slice.Start - 1);
            processor.Inline = new MdxCodeInline(content)
            {
                Span = new(spanStart, spanEnd),
                Line = line,
                Column = column,
                DelimiterCount = openSticks,
            };
            return true;
        }
    }
}
