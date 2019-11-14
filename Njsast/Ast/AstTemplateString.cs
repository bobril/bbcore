using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// A template string literal
    public class AstTemplateString : AstNode
    {
        /// [AstNode*] One or more segments, starting with AstTemplateSegment. AstNode may follow AstTemplateSegment, but each AstNode must be followed by AstTemplateSegment.
        public StructList<AstNode> Segments;

        public AstTemplateString(string? source, Position startLoc, Position endLoc, ref StructList<AstNode> segments) : base(source, startLoc, endLoc)
        {
            Segments.TransferFrom(ref segments);
        }

        AstTemplateString(string? source, Position startLoc, Position endLoc) : base(source, startLoc, endLoc)
        {
        }

        public override void Visit(TreeWalker w)
        {
            base.Visit(w);
            w.WalkList(Segments);
        }

        public override void Transform(TreeTransformer tt)
        {
            base.Transform(tt);
            tt.TransformList(ref Segments);
        }

        public override AstNode ShallowClone()
        {
            var res = new AstTemplateString(Source, Start, End);
            res.Segments.AddRange(Segments.AsReadOnlySpan());
            return res;
        }

        public override void CodeGen(OutputContext output)
        {
            var isTagged = output.Parent() is AstPrefixedTemplateString;

            output.Print("`");
            for (var i = 0u; i < Segments.Count; i++) {
                if (!(Segments[i] is AstTemplateSegment seg)) {
                    output.Print("${");
                    Segments[i].Print(output);
                    output.Print("}");
                } else if (isTagged) {
                    output.Print(seg.Raw);
                } else {
                    output.PrintStringChars(seg.Value, QuoteType.Template);
                }
            }
            output.Print("`");
        }
    }

}
