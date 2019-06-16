using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// Base class for loop control statements (`break` and `continue`)
    public class AstLoopControl : AstJump
    {
        /// [AstLabelRef?] the label, or null if none
        public AstLabelRef Label;

        public AstLoopControl(Parser parser, Position startPos, Position endPos, AstLabelRef label = null) : base(
            parser, startPos, endPos)
        {
            Label = label;
        }

        public override void Visit(TreeWalker w)
        {
            base.Visit(w);
            w.Walk(Label);
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
