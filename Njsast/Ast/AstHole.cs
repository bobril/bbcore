using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// A hole in an array
    public class AstHole : AstAtom
    {
        public AstHole(string? source, Position startLoc, Position endLoc) : base(source, startLoc, endLoc)
        {
        }

        public override AstNode ShallowClone()
        {
            return new AstHole(Source, Start, End);
        }

        public override void CodeGen(OutputContext output)
        {
        }
    }
}
