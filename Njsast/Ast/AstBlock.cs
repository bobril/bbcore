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

        public AstBlock(Parser parser, Position startPos, Position endPos) : base(parser, startPos, endPos)
        {
        }

        protected AstBlock(AstNode from) : base(from)
        {
        }

        public override void Visit(TreeWalker w)
        {
            base.Visit(w);
            w.WalkList(Body);
        }

        public override void CodeGen(OutputContext output)
        {
            output.PrintBraced(this, false);
        }

        public virtual bool IsBlockScope => true;
        public AstScope BlockScope { get; set; }
    }
}
