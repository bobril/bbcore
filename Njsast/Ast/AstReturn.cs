using Njsast.Reader;

namespace Njsast.Ast
{
    /// A `return` statement
    public class AstReturn : AstExit
    {
        public AstReturn(Parser parser, Position startPos, Position endPos, AstNode? value) : base(parser, startPos, endPos, value)
        {
        }

        public AstReturn(AstNode value) : base(value)
        {
        }
    }
}
