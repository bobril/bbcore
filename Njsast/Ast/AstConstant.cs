using Njsast.ConstEval;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// Base class for all constants
    public abstract class AstConstant : AstNode
    {
        protected AstConstant(string? source, Position startPos, Position endPos) : base(source, startPos, endPos)
        {
        }

        protected AstConstant()
        {
        }

        public override object? ConstValue(IConstEvalCtx? ctx = null)
        {
            return this;
        }
    }
}
