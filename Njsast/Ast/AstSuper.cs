using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// The `super` symbol
    public class AstSuper : AstThis
    {
        public AstSuper(Parser parser, Position startLoc, Position endLoc) : base(parser, startLoc, endLoc, "super")
        {
        }

        public override void CodeGen(OutputContext output)
        {
            output.Print("super");
        }
    }
}
