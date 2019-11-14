using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// The `Infinity` value
    public class AstInfinity : AstAtom
    {
        public AstInfinity(string? source, Position startLoc, Position endLoc) : base(source, startLoc, endLoc)
        {
        }

        public static readonly AstInfinity Instance = new AstInfinity(null, new Position(), new Position());
        public static readonly AstNode NegativeInstance = new AstUnaryPrefix(Operator.Subtraction, Instance);
        public static readonly object BoxedInfinity = double.PositiveInfinity;

        public override AstNode ShallowClone()
        {
            return new AstInfinity(Source, Start, End);
        }

        public override void CodeGen(OutputContext output)
        {
            output.Print("Infinity");
        }
    }
}
