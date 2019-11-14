using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// A `do` statement
    public class AstDo : AstDwLoop
    {
        public AstDo(string? source, Position startPos, Position endPos, AstNode test, AstStatement body)
            : base(source, startPos, endPos, test, body)
        {
        }

        public override AstNode ShallowClone()
        {
            return new AstDo(Source, Start, End, Condition, Body);
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
