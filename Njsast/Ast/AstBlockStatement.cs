using Njsast.Reader;

namespace Njsast.Ast
{
    /// A block statement
    public class AstBlockStatement : AstBlock
    {
        public AstBlockStatement(Parser parser, Position startPos, Position endPos) : base(parser, startPos, endPos)
        {
        }
    }
}
