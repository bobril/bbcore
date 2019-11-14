using Njsast.ConstEval;
using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// The `null` atom
    public class AstNull : AstAtom
    {
        public AstNull(string? source, Position startLoc, Position endLoc) : base(source, startLoc, endLoc)
        {
        }

        AstNull()
        {
        }

        public override AstNode ShallowClone()
        {
            return new AstNull(Source, Start, End);
        }

        public override void CodeGen(OutputContext output)
        {
            output.Print("null");
        }

        static readonly AstNull Instance = new AstNull();

        public override object? ConstValue(IConstEvalCtx? ctx = null)
        {
            return Instance;
        }
    }
}
