using Njsast.Reader;

namespace Njsast.Ast
{
    /// A `throw` statement
    public class AstThrow : AstExit
    {
        public AstThrow(Parser parser, Position startPos, Position endPos, AstNode value) : base(parser, startPos, endPos, value)
        {
        }
    }
}
