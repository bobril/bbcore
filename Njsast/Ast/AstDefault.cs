using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// A `default` switch branch
    public class AstDefault : AstSwitchBranch
    {
        public AstDefault(Parser parser, Position startPos, Position endPos) : base(parser, startPos, endPos)
        {
        }

        public override void CodeGen(OutputContext output)
        {
            output.Print("default:");
            output.Newline();
            for (var i = 0u; i < Body.Count; i++)
            {
                output.Indent();
                Body[i].Print(output);
                output.Newline();
            }
        }
    }
}
