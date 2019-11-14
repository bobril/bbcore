using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// An expandible argument, such as ...rest, a splat, such as [1,2,...all], or an expansion in a variable declaration, such as var [first, ...rest] = list
    public class AstExpansion : AstNode
    {
        /// [AstNode] the thing to be expanded
        public AstNode Expression;

        public AstExpansion(string? source, Position startLoc, Position endLoc, AstNode expression) : base(source,
            startLoc, endLoc)
        {
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

        public override AstNode ShallowClone()
        {
            return new AstExpansion(Source, Start, End, Expression);
        }

        public override void CodeGen(OutputContext output)
        {
            output.Print("...");
            Expression.Print(output);
        }
    }
}
