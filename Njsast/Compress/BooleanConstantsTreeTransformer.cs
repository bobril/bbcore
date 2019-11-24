using Njsast.Ast;
using Njsast.Reader;

namespace Njsast.Compress
{
    public class BooleanConstantsTreeTransformer : CompressModuleTreeTransformerBase
    {
        public BooleanConstantsTreeTransformer(ICompressOptions options) : base(options)
        {
        }
        
        protected override AstNode Before(AstNode node, bool inList)
        {
            switch (node)
            {
                case AstTrue _:
                    return CompressedTrueNode;
                case AstFalse _:
                    return CompressedFalseNode;
                default:
                    return node;
            }
        }

        protected override bool CanProcessNode(ICompressOptions options, AstNode node)
        {
            return options.EnableBooleanCompress && node is AstBoolean;
        }

        static AstUnaryPrefix CompressedTrueNode => new AstUnaryPrefix(Operator.LogicalNot, new AstNumber(0));

        static AstUnaryPrefix CompressedFalseNode => new AstUnaryPrefix(Operator.LogicalNot, new AstNumber(1));
    }
}