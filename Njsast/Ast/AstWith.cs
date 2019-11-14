using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// A `with` statement
    public class AstWith : AstStatementWithBody
    {
        /// [AstNode] the `with` expression
        public AstNode Expression;

        public AstWith(string? source, Position startPos, Position endPos, AstStatement body, AstNode expression) : base(
            source, startPos, endPos, body)
        {
            Expression = expression;
        }

        public override void Visit(TreeWalker w)
        {
            w.Walk(Expression);
            base.Visit(w);
        }

        public override void Transform(TreeTransformer tt)
        {
            Expression = tt.Transform(Expression);
            base.Transform(tt);
        }

        public override AstNode ShallowClone()
        {
            return new AstWith(Source, Start, End, Body, Expression);
        }

        public override void CodeGen(OutputContext output)
        {
            output.Print("with");
            output.Space();
            output.Print("(");
            Expression.Print(output);
            output.Print(")");
            output.Space();
            output.PrintBody(Body);
        }
    }
}
