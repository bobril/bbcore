using Njsast.Reader;

namespace Njsast.Ast
{
    /// A setter/getter function.  The `name` property is always null.
    public class AstAccessor : AstLambda
    {
        public AstAccessor(Parser parser, Position startPos, Position endPos, ref StructList<AstNode> argNames, bool isGenerator, bool async, ref StructList<AstNode> body) : base(parser, startPos, endPos, null, ref argNames, isGenerator, async, ref body)
        {
        }
    }
}
