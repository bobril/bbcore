using Njsast.Ast;

namespace Njsast.Compress
{
    class EmptyStatementEliminationTreeTransformer : CompressModuleTreeTransformerBase
    {
        public EmptyStatementEliminationTreeTransformer(ICompressOptions options) : base(options)
        {
        }
        
        protected override AstNode Before(AstNode node, bool inList)
        {
            return inList ? Remove : node;
        }

        protected override bool CanProcessNode(ICompressOptions options, AstNode node)
        {
            return options.EnableEmptyStatementElimination && node is AstEmptyStatement;
        }
    }
}