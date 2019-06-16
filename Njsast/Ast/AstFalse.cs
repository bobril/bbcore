using Njsast.ConstEval;
using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// The `false` atom
    public class AstFalse : AstBoolean
    {
        public AstFalse(Parser parser, Position startLoc, Position endLoc) : base(parser, startLoc, endLoc)
        {
        }

        public override void CodeGen(OutputContext output)
        {
            output.Print("false");
        }

        public static AstFalse Instance = new AstFalse(null, new Position(), new Position());

        public static readonly object BoxedFalse = false;

        public override object ConstValue(IConstEvalCtx ctx = null)
        {
            return BoxedFalse;
        }
    }
}
