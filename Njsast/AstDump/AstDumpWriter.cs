using Njsast.Ast;

namespace Njsast.AstDump
{
    public class AstDumpWriter : IAstDumpWriter
    {
        readonly ILineSink _lineSink;
        readonly bool _withoutPositions;

        public AstDumpWriter(ILineSink lineSink, bool withoutPositions = false)
        {
            _lineSink = lineSink;
            _withoutPositions = withoutPositions;
        }

        int _indent;
        string? _main;
        StructList<string> _propLines = new StructList<string>();

        public void Indent()
        {
            _lineSink.Print(_main!);
            foreach (var propLine in _propLines)
                _lineSink.Print(propLine);
            _propLines.Clear();
            _indent++;
        }

        public void Dedent()
        {
            _indent--;
        }

        public void Print(AstNode node)
        {
            if (_withoutPositions)
            {
                _main = new string(' ', _indent * 2) + node.GetType().Name.Substring(3);
            }
            else
            {
                _main = new string(' ', _indent * 2) + node.GetType().Name.Substring(3) + " " + (node.Start.Line + 1) +
                        ":" + (node.Start.Column + 1) + " - " + (node.End.Line + 1) + ":" + (node.End.Column + 1);
            }
        }

        public void PrintProp(string name, bool value)
        {
            if (value)
                _main += " [" + name + "]";
        }

        public void PrintProp(string name, string? value)
        {
            _propLines.Add(new string(' ', _indent * 2 + 2) + name + ": " + System.Web.HttpUtility.JavaScriptStringEncode(value));
        }
    }
}
