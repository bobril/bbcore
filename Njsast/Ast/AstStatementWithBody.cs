using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// Base class for all statements that contain one nested body: `For`, `ForIn`, `Do`, `While`, `With`
    public class AstStatementWithBody : AstStatement, IAstStatementWithBody
    {
        /// [AstStatement] the body; this should always be present, even if it's an AstEmptyStatement
        public AstStatement Body;

        protected AstStatementWithBody(Parser parser, Position startPos, Position endPos, AstStatement body) : base(parser,startPos,endPos)
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
