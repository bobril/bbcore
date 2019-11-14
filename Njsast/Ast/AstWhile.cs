using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// A `while` statement
    public class AstWhile : AstDwLoop
    {
        public AstWhile(string? source, Position startPos, Position endPos, AstNode test, AstStatement body) : base(
            source, startPos, endPos, test, body)
        {
        }

        public override AstNode ShallowClone()
        {
            return new AstWhile(Source, Start, End, Condition, Body);
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
