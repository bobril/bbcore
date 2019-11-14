using Njsast.Reader;

namespace Njsast.Ast
{
    /// Base class for do/while statements
    public abstract class AstDwLoop : AstIterationStatement
    {
        /// [AstNode] the loop condition.  Should not be instanceof AstStatement
        public AstNode Condition;

        protected AstDwLoop(string? source, Position startPos, Position endPos, AstNode test, AstStatement body)
        : base(source, startPos, endPos, body)
        {
            Condition = test;
        }

        public override void Visit(TreeWalker w)
        {
            w.Walk(Condition);
            base.Visit(w);
        }

        public override void Transform(TreeTransformer tt)
        {
            Condition = tt.Transform(Condition)!;
            base.Transform(tt);
        }
    }
}
