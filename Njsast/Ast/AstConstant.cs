using Njsast.ConstEval;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// Base class for all constants
    public abstract class AstConstant : AstNode
    {
        protected AstConstant(Parser parser, Position startLoc, Position endLoc) : base(parser, startLoc, endLoc)
        {
        }

        protected AstConstant()
        {
        }
        
        public override bool IsConstValue(IConstEvalCtx ctx = null)
        {
            return true;
        }

        public override object ConstValue(IConstEvalCtx ctx = null)
        {
            return this;
        }
    }
}
