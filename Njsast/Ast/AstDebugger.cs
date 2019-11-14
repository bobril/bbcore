using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// Represents a debugger statement
    public class AstDebugger : AstStatement
    {
        public AstDebugger(string? source, Position startLoc, Position endLoc) : base(source, startLoc, endLoc)
        {
        }

        public override AstNode ShallowClone()
        {
            return new AstDebugger(Source, Start, End);
        }

        public override void CodeGen(OutputContext output)
        {
            output.Print("debugger");
            output.Semicolon();
        }
    }
}
