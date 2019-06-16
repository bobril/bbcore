using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// A `finally` node; only makes sense as part of a `try` statement
    public class AstFinally : AstBlock
    {
        public AstFinally(Parser parser, Position startPos, Position endPos, ref StructList<AstNode> body) : base(
            parser, startPos, endPos, ref body)
        {
        }

        public override void CodeGen(OutputContext output)
        {
            output.Print("finally");
            output.Space();
            output.PrintBraced(this, false);
        }
    }
}
