using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// The `Infinity` value
    public class AstInfinity : AstAtom
    {
        public AstInfinity(Parser parser, Position startLoc, Position endLoc) : base(parser, startLoc, endLoc)
        {
        }

        public static readonly AstInfinity Instance = new AstInfinity(null, new Position(), new Position());
        public static AstNode NegativeInstance = new AstUnaryPrefix(Operator.Subtraction, Instance);
        public static readonly object BoxedInfinity = double.PositiveInfinity;

        public override void CodeGen(OutputContext output)
        {
            output.Print("Infinity");
        }
    }
}
