using Njsast.ConstEval;
using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// The `undefined` value
    public class AstUndefined : AstAtom
    {
        public AstUndefined(string? source, Position startLoc, Position endLoc) : base(source, startLoc, endLoc)
        {
        }

        public override AstNode ShallowClone()
        {
            return new AstUndefined(Source, Start, End);
        }

        public override void CodeGen(OutputContext output)
        {
            output.Print("undefined");
        }

        public static readonly AstUndefined Instance = new AstUndefined(null, new Position(), new Position());

        public override object? ConstValue(IConstEvalCtx? ctx = null)
        {
            return Instance;
        }
    }
}
