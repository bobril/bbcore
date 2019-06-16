using Njsast.Reader;

namespace Njsast.Ast
{
    /// A `for ... of` statement
    public class AstForOf : AstForIn
    {
        public AstForOf(Parser parser, Position startPos, Position endPos, AstStatement body, AstNode init, AstNode @object) : base(parser, startPos, endPos, body, init, @object)
        {
        }
    }
}
