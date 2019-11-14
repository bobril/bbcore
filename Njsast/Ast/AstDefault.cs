using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// A `default` switch branch
    public class AstDefault : AstSwitchBranch
    {
        public AstDefault(string? source, Position startPos, Position endPos) : base(source, startPos, endPos)
        {
        }

        public override AstNode ShallowClone()
        {
            var res = new AstDefault(Source, Start, End);
            res.Body.AddRange(Body.AsReadOnlySpan());
            return res;
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
