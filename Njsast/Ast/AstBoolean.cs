using Njsast.Reader;

namespace Njsast.Ast
{
    /// Base class for booleans
    public abstract class AstBoolean : AstAtom
    {
        public AstBoolean(Parser parser, Position startLoc, Position endLoc) : base(parser, startLoc, endLoc)
        {
        }
    }
}
