using Njsast.ConstEval;
using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// The `null` atom
    public class AstNull : AstAtom
    {
        public AstNull(Parser? parser, Position startLoc, Position endLoc) : base(parser, startLoc, endLoc)
        {
        }

        public override void CodeGen(OutputContext output)
        {
            output.Print("null");
        }

        static readonly AstNull Instance = new AstNull(null, new Position(), new Position());

        public override object? ConstValue(IConstEvalCtx? ctx = null)
        {
            return Instance;
        }
    }
}
