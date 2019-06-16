using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// A `while` statement
    public class AstWhile : AstDwLoop
    {
        public AstWhile(Parser parser, Position startPos, Position endPos, AstNode test, AstStatement body) : base(parser, startPos, endPos, test, body)
        {
        }

        public override void CodeGen(OutputContext output)
        {
            output.Print("while");
            output.Space();
            output.Print("(");
            Condition.Print(output);
            output.Print(")");
            output.Space();
            output.PrintBody(Body);
        }
    }
}
