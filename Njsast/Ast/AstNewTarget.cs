using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// A reference to new.target
    public class AstNewTarget : AstNode
    {
        public AstNewTarget(Parser parser, Position startLoc, Position endLoc) : base(parser, startLoc, endLoc)
        {
        }

        public override void CodeGen(OutputContext output)
        {
            output.Print("new.target");
        }
    }
}
