using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// Base class for “exits” (`return` and `throw`)
    public abstract class AstExit : AstJump
    {
        /// [AstNode?] the value returned or thrown by this statement; could be null for AstReturn
        public AstNode? Value;

        protected AstExit(string? source, Position startPos, Position endPos, AstNode? value) : base(source, startPos,
            endPos)
        {
            Value = value;
        }

        protected AstExit(AstNode? value)
        {
            Value = value;
        }

        public override void Visit(TreeWalker w)
        {
            base.Visit(w);
            w.Walk(Value);
        }

        public override void Transform(TreeTransformer tt)
        {
            base.Transform(tt);
            if (Value != null)
                Value = tt.Transform(Value)!;
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
