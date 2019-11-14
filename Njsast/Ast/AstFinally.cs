using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// A `finally` node; only makes sense as part of a `try` statement
    public class AstFinally : AstBlock
    {
        public AstFinally(string? source, Position startPos, Position endPos, ref StructList<AstNode> body) : base(
            source, startPos, endPos, ref body)
        {
        }

        AstFinally(string? source, Position startPos, Position endPos) : base(
            source, startPos, endPos)
        {
        }

        public override void CodeGen(OutputContext output)
        {
            output.Print("finally");
            output.Space();
            output.PrintBraced(this, false);
        }

        public override AstNode ShallowClone()
        {
            var res = new AstFinally(Source, Start, End);
            res.Body.AddRange(Body.AsReadOnlySpan());
            return res;
        }
    }
}
