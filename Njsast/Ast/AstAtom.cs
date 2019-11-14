using Njsast.Reader;

namespace Njsast.Ast
{
    /// Base class for atoms
    public abstract class AstAtom : AstConstant
    {
        protected AstAtom(string? source, Position startPos, Position endPos) : base(source, startPos, endPos)
        {
        }

        protected AstAtom()
        {
        }
    }
}
