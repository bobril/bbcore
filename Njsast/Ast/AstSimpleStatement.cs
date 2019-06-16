using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// A statement consisting of an expression, i.e. a = 1 + 2
    public class AstSimpleStatement : AstStatement, IAstStatementWithBody
    {
        /// [AstNode] an expression node (should not be instanceof AstStatement)
        public AstNode Body;

        public AstSimpleStatement(Parser parser, Position startPos, Position endPos, AstNode body) : base(parser, startPos, endPos)
        {
            Body = body;
        }

        public override void Visit(TreeWalker w)
        {
            base.Visit(w);
            w.Walk(Body);
        }

        public override void CodeGen(OutputContext output)
        {
            Body.Print(output);
            output.Semicolon();
        }

        public AstNode GetBody()
        {
            return Body;
        }
    }
}
