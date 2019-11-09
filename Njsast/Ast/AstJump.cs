using Njsast.Reader;

namespace Njsast.Ast
{
    /// Base class for “jumps” (for now that's `return`, `throw`, `break` and `continue`)
    public class AstJump : AstStatement
    {
        public AstJump(Parser parser, Position startPos, Position endPos) : base(parser, startPos, endPos)
        {
        }

        protected AstJump()
        {
        }
    }
}
