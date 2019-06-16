using System.Text.RegularExpressions;

namespace Njsast.Reader
{
    public sealed partial class Parser
    {
        static readonly Regex LineBreak = new Regex("\r\n?|\n|\u2028|\u2029");
        static readonly Regex NonAsciIwhitespace = new Regex(@"[\u1680\u180e\u2000-\u200a\u202f\u205f\u3000\ufeff]");
        static readonly Regex SkipWhiteSpace = new Regex(@"(?:\s|\/\/.*|\/\*.*?\*\/)*");

        static bool IsNewLine(char code)
        {
            return code == 10 || code == 13 || code == 0x2028 || code == 0x2029;
        }
    }
}
