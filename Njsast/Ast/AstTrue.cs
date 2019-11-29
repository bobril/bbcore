using Njsast.ConstEval;
using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// The `true` atom
    public class AstTrue : AstBoolean
    {
        public AstTrue(string? source, Position startLoc, Position endLoc) : base(source, startLoc, endLoc)
        {
        }

        public override AstNode ShallowClone()
        {
            return new AstTrue(Source, Start, End);
        }

        public override void CodeGen(OutputContext output)
        {
            if (!output.Options.ShortenBooleans)
                output.Print("true");
            else
            {
                output.Print("!0");
                output.SetNeedDotAfterNumber();
            }
        }

        public static AstTrue Instance = new AstTrue(null, new Position(), new Position());

        public static readonly object BoxedTrue = true;

        public override object? ConstValue(IConstEvalCtx? ctx = null)
        {
            return BoxedTrue;
        }
    }
}
