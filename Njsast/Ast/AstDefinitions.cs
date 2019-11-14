using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// Base class for `var` or `const` nodes (variable declarations/initializations)
    public abstract class AstDefinitions : AstStatement
    {
        /// [AstVarDef*] array of variable definitions
        public StructList<AstVarDef> Definitions;

        protected AstDefinitions(string? source, Position startPos, Position endPos, ref StructList<AstVarDef> definitions)
            : base(source, startPos, endPos)
        {
            Definitions.TransferFrom(ref definitions);
        }

        protected AstDefinitions(string? source, Position startPos, Position endPos)
            : base(source, startPos, endPos)
        {
        }

        protected AstDefinitions(ref StructList<AstVarDef> definitions)
        {
            Definitions.TransferFrom(ref definitions);
        }

        protected AstDefinitions(AstNode from) : base(from)
        {
        }

        public override void Visit(TreeWalker w)
        {
            base.Visit(w);
            w.WalkList(Definitions);
        }

        public override void Transform(TreeTransformer tt)
        {
            base.Transform(tt);
            tt.TransformList(ref Definitions);
        }

        protected void DoPrint(OutputContext output, string kind)
        {
            output.Print(kind);
            output.Space();
            for (var i = 0u; i < Definitions.Count; i++)
            {
                if (i > 0)
                    output.Comma();
                Definitions[i].Print(output);
            }

            var p = output.Parent();
            if (p is AstFor astFor && astFor.Init == this) return;
            if (p is AstForIn astForIn && astForIn.Init == this) return;
            if (p == null) return;
            output.Semicolon();
        }
    }
}
