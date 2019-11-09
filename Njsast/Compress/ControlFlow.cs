using Njsast.Ast;

namespace Njsast.Compress
{
    public class ControlFlow
    {
        public AstStatementWithBody Statement { get; }

        public AstBlock Parent { get; }

        public ControlFlow(AstStatementWithBody statement, AstBlock parent)
        {
            Statement = statement;
            Parent = parent;
        }
    }
}