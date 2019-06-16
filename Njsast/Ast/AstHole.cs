using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// A hole in an array
    public class AstHole : AstAtom
    {
        public AstHole(Parser parser, Position startLoc, Position endLoc) : base(parser, startLoc, endLoc)
        {
        }

        public override void CodeGen(OutputContext output)
        {
        }
    }
}
