using System.Text.RegularExpressions;

namespace Njsast.Reader
{
    public sealed partial class Parser
    {
        /**
         * \u2028 LINE SEPARATOR
         * \u2029 PARAGRAPH SEPARATOR
         */
        static readonly Regex LineBreak = new Regex("\r\n?|\n|\u2028|\u2029");
        /**
         * \u1680 OGHAM SPACE MARK
         * \u2000 EN QUAD
         * \u2001 EM QUAD
         * \u2002 EN SPACE
         * \u2003 EM SPACE
         * \u2004 THREE-PER-EM SPACE
         * \u2005 FOUR-PER-EM SPACE
         * \u2006 SIX-PER-EM SPACE
         * \u2007 FIGURE SPACE
         * \u2008 PUNCTUATION SPACE
         * \u2009 THIN SPACE
         * \u200A HAIR SPACE
         * \u202F NARROW NO-BREAK SPACE
         * \u205F MEDIUM MATHEMATICAL SPACE
         * \u3000 IDEOGRAPHIC SPACE
         * \uFEFF ZERO WIDTH NO-BREAK SPACE
         */
        // Note \u180e MONGOLIAN VOWEL SEPARATOR Is no longer classified as space character (Unicode >= 6.3.0) =>
        // https://en.wikipedia.org/wiki/Whitespace_character
        // and is not in "Zs" unicode category => https://www.compart.com/en/unicode/category/Zs 
        // https://www.ecma-international.org/ecma-262/10.0/index.html#table-32
        static readonly Regex NonAsciIwhitespace = new Regex(@"[\u1680\u2000-\u200a\u202f\u205f\u3000\ufeff]");
        static readonly Regex SkipWhiteSpace = new Regex(@"(?:\s|\/\/.*|\/\*.*?\*\/)*");

        static bool IsNewLine(char code)
        {
            return code == CharCode.LineFeed || code == CharCode.CarriageReturn || code == CharCode.LineSeparator || code == CharCode.ParagraphSeparator;
        }
    }
}
