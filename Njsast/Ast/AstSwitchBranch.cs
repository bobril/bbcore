using Njsast.Reader;

namespace Njsast.Ast
{
    /// Base class for `switch` branches
    public abstract class AstSwitchBranch : AstBlock
    {
        protected AstSwitchBranch(string? source, Position startPos, Position endPos) : base(source, startPos, endPos)
        {
        }

        public override bool IsBlockScope => false;
    }
}
