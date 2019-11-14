using Njsast.Reader;

namespace Njsast.Ast
{
    /// A `for ... of` statement
    public class AstForOf : AstForIn
    {
        public AstForOf(string? source, Position startPos, Position endPos, AstStatement body, AstNode init, AstNode @object) : base(source, startPos, endPos, body, init, @object)
        {
        }
    }
}
