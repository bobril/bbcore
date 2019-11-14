using Njsast.Reader;

namespace Njsast.Ast
{
    /// A block statement
    public class AstBlockStatement : AstBlock
    {
        public AstBlockStatement(string? source, Position startPos, Position endPos) : base(source, startPos, endPos)
        {
        }
    }
}
