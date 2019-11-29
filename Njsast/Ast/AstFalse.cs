using Njsast.ConstEval;
using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// The `false` atom
    public class AstFalse : AstBoolean
    {
        public AstFalse(string? source, Position startLoc, Position endLoc) : base(source, startLoc, endLoc)
        {
        }

        public override AstNode ShallowClone()
        {
            return new AstFalse(Source, Start, End);
        }

        public override void CodeGen(OutputContext output)
        {
            if (!output.Options.ShortenBooleans)
                output.Print("false");
            else
            {
                output.Print("!1");
                output.SetNeedDotAfterNumber();
            }
        }

        public static AstFalse Instance = new AstFalse(null, new Position(), new Position());

        public static readonly object BoxedFalse = false;

        public override object? ConstValue(IConstEvalCtx? ctx = null)
        {
            return BoxedFalse;
        }
    }
}
