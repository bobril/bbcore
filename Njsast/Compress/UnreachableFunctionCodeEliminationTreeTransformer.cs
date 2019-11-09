using Njsast.Ast;

namespace Njsast.Compress
{
    public class UnreachableFunctionCodeEliminationTreeTransformer : UnreachableAfterJumpCodeEliminationTreeTransformerBase<AstLambda, AstExit>
    {
        public UnreachableFunctionCodeEliminationTreeTransformer(ICompressOptions options) : base(options)
        {
        }
    }
}