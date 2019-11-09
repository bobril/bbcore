using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    // Base class of all statements
    public class AstStatement : AstNode
    {
        public AstStatement(Parser? parser, Position startPos, Position endPos) : base(parser, startPos, endPos)
        {
        }

        protected AstStatement(AstNode from) : base(from)
        {
        }

        protected AstStatement()
        {
        }

        public override void CodeGen(OutputContext output)
        {
            throw new System.InvalidOperationException();
        }
    }
}
