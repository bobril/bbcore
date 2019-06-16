using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// The `this` symbol
    public class AstThis : AstSymbol
    {
        public AstThis(Parser parser, Position startLoc, Position endLoc) : base(parser, startLoc, endLoc, "this")
        {
        }

        protected AstThis(Parser parser, Position startLoc, Position endLoc, string super) : base(parser, startLoc, endLoc, super)
        {
        }

        public override void CodeGen(OutputContext output)
        {
            output.Print("this");
        }
    }
}
