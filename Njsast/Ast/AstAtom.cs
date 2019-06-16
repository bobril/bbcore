using Njsast.Reader;

namespace Njsast.Ast
{
    /// Base class for atoms
    public abstract class AstAtom : AstConstant
    {
        public AstAtom(Parser parser, Position startLoc, Position endLoc) : base(parser, startLoc, endLoc)
        {
        }
    }
}
