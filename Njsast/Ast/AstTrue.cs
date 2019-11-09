using Njsast.ConstEval;
using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// The `true` atom
    public class AstTrue : AstBoolean
    {
        public AstTrue(Parser? parser, Position startLoc, Position endLoc) : base(parser, startLoc, endLoc)
        {
        }

        public override void CodeGen(OutputContext output)
        {
            output.Print("true");
        }

        public static AstTrue Instance = new AstTrue(null, new Position(), new Position());

        public static readonly object BoxedTrue = true;

        public override object? ConstValue(IConstEvalCtx? ctx = null)
        {
            return BoxedTrue;
        }
    }
}
