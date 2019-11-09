using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// The impossible value
    public class AstNaN : AstAtom
    {
        public AstNaN(Parser? parser, Position startLoc, Position endLoc) : base(parser, startLoc, endLoc)
        {
        }

        public static readonly AstNaN Instance = new AstNaN(null, new Position(), new Position());

        public static readonly object BoxedNaN = double.NaN;

        public override void CodeGen(OutputContext output)
        {
            output.Print("NaN");
        }
    }
}
