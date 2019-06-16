using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// Represents a debugger statement
    public class AstDebugger : AstStatement
    {
        public AstDebugger(Parser parser, Position startLoc, Position endLoc) : base(parser, startLoc, endLoc)
        {
        }

        public override void CodeGen(OutputContext output)
        {
            output.Print("debugger");
            output.Semicolon();
        }
    }
}
