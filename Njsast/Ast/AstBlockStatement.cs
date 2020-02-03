using Njsast.Reader;

namespace Njsast.Ast
{
    /// A block statement
    public class AstBlockStatement : AstBlock
    {
        public AstBlockStatement(string? source, Position startPos, Position endPos, ref StructList<AstNode> body) : base(source, startPos, endPos, ref body)
        {
        }

        AstBlockStatement(string? source, Position startPos, Position endPos) : base(source, startPos, endPos)
        {
        }

        public AstBlockStatement(AstNode from): base(from)
        {
        }

        public override AstNode ShallowClone()
        {
            var res = new AstBlockStatement(Source, Start, End);
            res.Body.AddRange(Body.AsReadOnlySpan());
            return res;
        }
    }
}
