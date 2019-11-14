using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// Base class for loop control statements (`break` and `continue`)
    public abstract class AstLoopControl : AstJump
    {
        /// [AstLabelRef?] the label, or null if none
        public AstLabelRef? Label;

        protected AstLoopControl(string? source, Position startPos, Position endPos, AstLabelRef? label = null) : base(
            source, startPos, endPos)
        {
            Label = label;
        }

        public override void Visit(TreeWalker w)
        {
            base.Visit(w);
            w.Walk(Label);
        }

        public override void Transform(TreeTransformer tt)
        {
            base.Transform(tt);
            if (Label != null)
                Label = (AstLabelRef)tt.Transform(Label);
        }

        public override void CodeGen(OutputContext output)
        {
            output.Print(this is AstBreak ? "break" : "continue");
            if (Label != null)
            {
                output.Space();
                var name = Label.Thedef?.MangledName ?? Label.Thedef?.Name ?? Label.Name;
                output.PrintName(name);
            }

            output.Semicolon();
        }
    }
}
