using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// A `do` statement
    public class AstDo : AstDwLoop
    {
        public AstDo(Parser parser, Position startPos, Position endPos, AstNode test, AstStatement body)
            : base(parser, startPos, endPos, test, body)
        {
        }

        public override void CodeGen(OutputContext output)
        {
            output.Print("do");
            output.Space();
            output.MakeBlock(Body);
            output.Space();
            output.Print("while");
            output.Space();
            output.Print("(");
            Condition.Print(output);
            output.Print(")");
            output.Semicolon();
        }
    }
}
