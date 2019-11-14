using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// An `await` statement
    public class AstAwait : AstNode
    {
        /// [AstNode] the mandatory expression being awaited
        public AstNode Expression;

        public AstAwait(string? source, Position startLoc, Position endLoc, AstNode expression) :
            base(source, startLoc, endLoc)
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
            return new AstAwait(Source, Start, End, Expression);
        }

        public override void CodeGen(OutputContext output)
        {
            output.Print("await");
            output.Space();
            var parens = !(
                Expression is AstCall
                || Expression is AstSymbolRef
                || Expression is AstPropAccess
                || Expression is AstUnary
                || Expression is AstConstant
            );
            Expression.Print(output, parens);
        }

        public override bool NeedParens(OutputContext output)
        {
            var p = output.Parent();
            return p is AstPropAccess propAccess && propAccess.Expression == this
                   || p is AstCall call && call.Expression == this
                   || output.Options.Safari10 && p is AstUnaryPrefix;
        }
    }
}
