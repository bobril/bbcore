using Njsast.Reader;

namespace Njsast.Ast
{
    /// A default assignment expression like in `(a = 3) => a`
    public class AstDefaultAssign : AstBinary
    {
        public AstDefaultAssign(Parser parser, Position startLoc, Position endLoc, AstNode left, AstNode right) : base(parser, startLoc, endLoc, left, right, Operator.Assignment)
        {
        }
    }
}
