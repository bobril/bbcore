using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// A `for` statement
    public class AstFor : AstIterationStatement
    {
        /// [AstNode?] the `for` initialization code, or null if empty
        public AstNode Init;

        /// [AstNode?] the `for` termination clause, or null if empty
        public AstNode Condition;

        /// [AstNode?] the `for` update clause, or null if empty
        public AstNode Step;

        public AstFor(Parser parser, Position startPos, Position endPos, AstStatement body, AstNode init,
            AstNode condition, AstNode step) : base(parser, startPos, endPos, body)
        {
            Init = init;
            Condition = condition;
            Step = step;
        }

        public override void Visit(TreeWalker w)
        {
            w.Walk(Init);
            w.Walk(Condition);
            w.Walk(Step);
            base.Visit(w);
        }

        public override void CodeGen(OutputContext output)
        {
            output.Print("for");
            output.Space();
            output.Print("(");
            if (Init != null)
            {
                if (Init is AstDefinitions)
                {
                    Init.Print(output);
                }
                else
                {
                    output.ParenthesizeForNoin(Init, true);
                }

                output.Print(";");
                output.Space();
            }
            else
            {
                output.Print(";");
            }

            if (Condition != null)
            {
                Condition.Print(output);
                output.Print(";");
                output.Space();
            }
            else
            {
                output.Print(";");
            }

            if (Step != null)
            {
                Step.Print(output);
            }

            output.Print(")");
            output.Space();
            output.PrintBody(Body);
        }
    }
}
