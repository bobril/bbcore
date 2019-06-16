using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// Base class for “exits” (`return` and `throw`)
    public class AstExit : AstJump
    {
        /// [AstNode?] the value returned or thrown by this statement; could be null for AstReturn
        public AstNode Value;

        public AstExit(Parser parser, Position startPos, Position endPos, AstNode value) : base(parser, startPos,
            endPos)
        {
            Value = value;
        }

        public override void Visit(TreeWalker w)
        {
            base.Visit(w);
            w.Walk(Value);
        }

        public override void CodeGen(OutputContext output)
        {
            output.Print(this is AstReturn ? "return" : "throw");
            if (Value != null)
            {
                output.Space();
                Value.Print(output);
            }

            output.Semicolon();
        }
    }
}
