using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// An array literal
    public class AstArray : AstNode
    {
        /// [AstNode*] array of elements
        public StructList<AstNode> Elements;

        public AstArray(string? source, Position startLoc, Position endLoc, ref StructList<AstNode> elements) : base(
            source, startLoc, endLoc)
        {
            Elements.TransferFrom(ref elements);
        }

        AstArray(string? source, Position startLoc, Position endLoc) : base(source, startLoc, endLoc)
        {
        }

        public AstArray()
        {
        }

        public override void Visit(TreeWalker w)
        {
            base.Visit(w);
            w.WalkList(Elements);
        }

        public override void Transform(TreeTransformer tt)
        {
            base.Transform(tt);
            tt.TransformList(ref Elements);
        }

        public override AstNode ShallowClone()
        {
            var res = new AstArray(Source, Start, End);
            res.Elements.AddRange(Elements.AsReadOnlySpan());
            return res;
        }

        public override void CodeGen(OutputContext output)
        {
            output.Print("[");
            var len = Elements.Count;
            if (len > 0) output.Space();
            for (var i = 0u; i < Elements.Count; i++)
            {
                if (i > 0) output.Comma();
                var exp = Elements[i];
                exp.Print(output);
                // If the final element is a hole, we need to make sure it
                // doesn't look like a trailing comma, by inserting an actual
                // trailing comma.
                if (i == len - 1 && exp is AstHole)
                    output.Comma();
            }

            if (len > 0) output.Space();
            output.Print("]");
        }
    }
}
