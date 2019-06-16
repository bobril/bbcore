using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// A `case` switch branch
    public class AstCase : AstSwitchBranch
    {
        /// [AstNode] the `case` expression
        public AstNode Expression;

        public AstCase(Parser parser, Position startPos, Position endPos, AstNode expression) : base(parser, startPos,
            endPos)
        {
            Expression = expression;
        }

        public override void Visit(TreeWalker w)
        {
            base.Visit(w);
            w.Walk(Expression);
        }

        public override void CodeGen(OutputContext output)
        {
            output.Print("case");
            output.Space();
            Expression.Print(output);
            output.Print(":");
            output.Newline();
            for (var i = 0u; i < Body.Count; i++)
            {
                output.Indent();
                Body[i].Print(output);
                output.Newline();
            }
        }
    }
}
