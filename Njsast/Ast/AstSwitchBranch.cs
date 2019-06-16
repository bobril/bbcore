using Njsast.Reader;

namespace Njsast.Ast
{
    /// Base class for `switch` branches
    public class AstSwitchBranch : AstBlock
    {
        public AstSwitchBranch(Parser parser, Position startPos, Position endPos) : base(parser, startPos, endPos)
        {
        }

        public override bool IsBlockScope => false;
    }
}
