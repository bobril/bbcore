using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// A body of statements (usually bracketed)
    public class AstBlock : AstStatement, IMayBeBlockScope
    {
        /// [AstStatement*] an array of statements
        public StructList<AstNode> Body;

        public AstBlock(Parser parser, Position startPos, Position endPos, ref StructList<AstNode> body) : base(
            parser, startPos, endPos)
        {
            Body.TransferFrom(ref body);
        }

        protected AstBlock(Parser parser, Position startPos, Position endPos) : base(parser, startPos, endPos)
        {
        }

        public AstBlock(AstNode from) : base(from)
        {
        }

        protected AstBlock()
        {
        }

        public override void Visit(TreeWalker w)
        {
            base.Visit(w);
            w.WalkList(Body);
        }

        public override void Transform(TreeTransformer tt)
        {
            base.Transform(tt);
            tt.TransformList(ref Body);
        }

        public override void CodeGen(OutputContext output)
        {
            output.PrintBraced(this, false);
        }

        public virtual bool IsBlockScope => true;
        public AstScope? BlockScope { get; set; }
    }
}
