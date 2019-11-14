using Njsast.AstDump;
using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// Base class for unary expressions
    public abstract class AstUnary : AstNode
    {
        public Operator Operator;

        /// [AstNode] expression that this unary operator applies to
        public AstNode Expression;

        protected AstUnary(string? source, Position startLoc, Position endLoc, Operator @operator, AstNode expression) :
            base(source, startLoc, endLoc)
        {
            Operator = @operator;
            Expression = expression;
        }

        protected AstUnary(Operator @operator, AstNode expression)
        {
            Operator = @operator;
            Expression = expression;
        }

        public override void Visit(TreeWalker w)
        {
            base.Visit(w);
            w.Walk(Expression);
        }

        public override void Transform(TreeTransformer tt)
        {
            base.Transform(tt);
            Expression = tt.Transform(Expression)!;
        }

        public override void DumpScalars(IAstDumpWriter writer)
        {
            base.DumpScalars(writer);
            writer.PrintProp("Operator", Operator.ToString());
        }

        public override bool NeedParens(OutputContext output)
        {
            var p = output.Parent();
            return p is AstPropAccess propAccess && propAccess.Expression == this
                   || p is AstCall call && call.Expression == this
                   || p is AstBinary binary
                   && binary.Operator == Operator.Power
                   && this is AstUnaryPrefix thisUnaryPrefix
                   && binary.Left == this
                   && thisUnaryPrefix.Operator != Operator.Increment
                   && thisUnaryPrefix.Operator != Operator.Decrement;
        }
    }
}
