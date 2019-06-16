using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// The empty statement (empty block or simply a semicolon)
    public class AstEmptyStatement : AstStatement
    {
        public AstEmptyStatement(Parser parser, Position startPos, Position endPos) : base(parser, startPos, endPos)
        {
        }

        public override void CodeGen(OutputContext output)
        {
            output.Semicolon();
        }
    }
}
