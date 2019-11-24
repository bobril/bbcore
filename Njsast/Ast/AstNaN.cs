using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// The impossible value
    public class AstNaN : AstAtom
    {
        public AstNaN(string? source, Position startLoc, Position endLoc) : base(source, startLoc, endLoc)
        {
        }

        public static readonly AstNaN Instance = new AstNaN(null, new Position(), new Position());

        public static readonly object BoxedNaN = double.NaN;

        public override AstNode ShallowClone()
        {
            return new AstNaN(Source, Start, End);
        }

        public override void CodeGen(OutputContext output)
        {
            output.Print("NaN");
        }
    }
}
