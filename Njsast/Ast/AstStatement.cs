using Njsast.Reader;

namespace Njsast.Ast
{
    // Base class of all statements
    public abstract class AstStatement : AstNode
    {
        protected AstStatement(string? source, Position startPos, Position endPos) : base(source, startPos, endPos)
        {
        }

        protected AstStatement(AstNode from) : base(from)
        {
        }

        protected AstStatement()
        {
        }
    }
}
