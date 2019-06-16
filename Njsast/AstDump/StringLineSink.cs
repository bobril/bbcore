using System.Text;

namespace Njsast.AstDump
{
    public class StringLineSink : ILineSink
    {
        readonly StringBuilder _builder = new StringBuilder();

        public void Print(string line)
        {
            _builder.Append(line);
            _builder.Append('\n');
        }

        public override string ToString()
        {
            return _builder.ToString();
        }
    }
}
