using Njsast.Ast;

namespace Njsast.Compress
{
    public class UnreachableLoopCodeEliminationTreeTransformer : UnreachableAfterJumpCodeEliminationTreeTransformerBase<AstIterationStatement, AstLoopControl>
    {
        public UnreachableLoopCodeEliminationTreeTransformer(ICompressOptions options) : base(options)
        {
        }

        protected override AstLoopControl ProcessJumpNode(AstLoopControl node)
        {
            if (IsProcessingSwitchStatement && node is AstBreak)
                return node;
            return base.ProcessJumpNode(node);
        }
    }
}