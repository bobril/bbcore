using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// A `switch` statement
    public class AstSwitch : AstBlock
    {
        /// [AstNode] the `switch` “discriminant”
        public AstNode Expression;

        public AstSwitch(string? source, Position startPos, Position endPos, AstNode expression,
            ref StructList<AstNode> body) : base(source, startPos, endPos, ref body)
        {
            Expression = expression;
        }

        AstSwitch(string? source, Position startPos, Position endPos, AstNode expression) : base(source, startPos, endPos)
        {
            Expression = expression;
        }

        public override AstNode ShallowClone()
        {
            var res = new AstSwitch(Source, Start, End, Expression);
            res.Body.AddRange(Body.AsReadOnlySpan());
            return res;
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

        public override void CodeGen(OutputContext output)
        {
            output.Print("switch");
            output.Space();
            Expression.Print(output, true);
            output.Space();
            if (Body.Count == 0)
                output.Print("{}");
            else
            {
                output.Print("{");
                output.Newline();
                output.Indentation += output.Options.IndentLevel;
                for (var i = 0u; i < Body.Count; i++)
                {
                    var branch = (AstSwitchBranch)Body[i];
                    output.Indent(true);
                    branch.Print(output);
                    if (i < Body.Count-1 && branch.Body.Count > 0)
                        output.Newline();
                }
                output.Indentation -= output.Options.IndentLevel;
                output.Indent();
                output.Print("}");
            }
        }
    }
}
