using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// The empty statement (empty block or simply a semicolon)
    public class AstEmptyStatement : AstStatement
    {
        public AstEmptyStatement(string? source, Position startPos, Position endPos) : base(source, startPos, endPos)
        {
        }

        public AstEmptyStatement()
        {
        }

        public override AstNode ShallowClone()
        {
            return new AstEmptyStatement(Source, Start, End);
        }

        public override void CodeGen(OutputContext output)
        {
            output.Semicolon();
        }
    }
}
