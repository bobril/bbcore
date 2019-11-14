using Njsast.Reader;

namespace Njsast.Ast
{
    /// Base class for booleans
    public abstract class AstBoolean : AstAtom
    {
        public AstBoolean(string? source, Position startLoc, Position endLoc) : base(source, startLoc, endLoc)
        {
        }
    }
}
