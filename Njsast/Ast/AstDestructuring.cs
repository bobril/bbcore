using Njsast.AstDump;
using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// A destructuring of several names. Used in destructuring assignment and with destructuring function argument names
    public class AstDestructuring : AstNode
    {
        /// [AstNode*] Array of properties or elements
        public StructList<AstNode> Names;

        /// [Boolean] Whether the destructuring represents an object or array
        public bool IsArray;

        public AstDestructuring(string? source, Position startLoc, Position endLoc, ref StructList<AstNode> names,
            bool isArray) : base(source, startLoc, endLoc)
        {
            Names.TransferFrom(ref names);
            IsArray = isArray;
        }

        AstDestructuring(string? source, Position startLoc, Position endLoc, bool isArray) : base(source, startLoc, endLoc)
        {
            IsArray = isArray;
        }

        public override void Visit(TreeWalker w)
        {
            base.Visit(w);
            w.WalkList(Names);
        }

        public override void Transform(TreeTransformer tt)
        {
            base.Transform(tt);
            tt.TransformList(ref Names);
        }

        public override void DumpScalars(IAstDumpWriter writer)
        {
            base.DumpScalars(writer);
            writer.PrintProp("IsArray", IsArray);
        }

        public override AstNode ShallowClone()
        {
            var res = new AstDestructuring(Source, Start, End, IsArray);
            res.Names.AddRange(Names.AsReadOnlySpan());
            return res;
        }

        public override void CodeGen(OutputContext output)
        {
            output.Print(IsArray ? "[" : "{");
            var len = Names.Count;
            for (var i = 0; i < len; i++)
            {
                var name = Names[(uint) i];
                if (i > 0) output.Comma();
                name.Print(output);
                // If the final element is a hole, we need to make sure it
                // doesn't look like a trailing comma, by inserting an actual
                // trailing comma.
                if (i == len - 1 && name is AstHole) output.Comma();
            }

            output.Print(IsArray ? "]" : "}");
        }
    }
}
