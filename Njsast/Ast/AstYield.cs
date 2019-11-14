using Njsast.AstDump;
using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// A `yield` statement
    public class AstYield : AstNode
    {
        /// [AstNode?] the value returned or thrown by this statement; could be null (representing undefined) but only when is_star is set to false
        public AstNode? Expression;

        /// [Boolean] Whether this is a yield or yield* statement
        public bool IsStar;

        public AstYield(string? source, Position startLoc, Position endLoc, AstNode? expression, bool isStar) : base(
            source, startLoc, endLoc)
        {
            if (isStar && expression == null)
            {
                throw Parser.NewSyntaxError(startLoc, "Expression is missing in yield*");
            }
            Expression = expression;
            IsStar = isStar;
        }

        public override void Visit(TreeWalker w)
        {
            base.Visit(w);
            w.Walk(Expression);
        }

        public override void Transform(TreeTransformer tt)
        {
            base.Transform(tt);
            if (Expression != null)
                Expression = tt.Transform(Expression);
        }

        public override void DumpScalars(IAstDumpWriter writer)
        {
            base.DumpScalars(writer);
            writer.PrintProp("IsStar", IsStar);
        }

        public override AstNode ShallowClone()
        {
            return new AstYield(Source, Start, End, Expression, IsStar);
        }

        public override void CodeGen(OutputContext output)
        {
            output.Print(IsStar ? "yield*" : "yield");
            if (Expression != null)
            {
                output.Space();
                Expression.Print(output);
            }
        }

        public override bool NeedParens(OutputContext output)
        {
            var p = output.Parent();
            // (yield 1) + (yield 2)
            // a = yield 3
            if (p is AstBinary binary && binary.Operator != Operator.Assignment)
                return true;
            // (yield 1)()
            // new (yield 1)()
            if (p is AstCall call && call.Expression == this)
                return true;
            // (yield 1) ? yield 2 : yield 3
            if (p is AstConditional conditional && conditional.Condition == this)
                return true;
            // -(yield 4)
            if (p is AstUnary)
                return true;
            // (yield x).foo
            // (yield x)['foo']
            if (p is AstPropAccess propAccess && propAccess.Expression == this)
                return true;
            return false;
        }
    }
}
