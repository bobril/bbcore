using Njsast.Reader;

namespace Njsast.Ast
{
    /// Base class for “jumps” (for now that's `return`, `throw`, `break` and `continue`)
    public abstract class AstJump : AstStatement
    {
        protected AstJump(string? source, Position startPos, Position endPos) : base(source, startPos, endPos)
        {
        }

        protected AstJump()
        {
        }
    }
}
