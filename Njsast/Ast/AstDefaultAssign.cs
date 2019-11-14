using Njsast.Reader;

namespace Njsast.Ast
{
    /// A default assignment expression like in `(a = 3) => a`
    public class AstDefaultAssign : AstBinary
    {
        public AstDefaultAssign(string? source, Position startLoc, Position endLoc, AstNode left, AstNode right) : base(source, startLoc, endLoc, left, right, Operator.Assignment)
        {
        }
    }
}
