using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// A `catch` node; only makes sense as part of a `try` statement
    public class AstCatch : AstBlock
    {
        /// [AstSymbolCatch|AstDestructuring|AstExpansion|AstDefaultAssign] symbol for the exception
        public AstNode Argname;

        public AstCatch(string? source, Position startPos, Position endPos, AstNode argname,
            ref StructList<AstNode> body) : base(source, startPos, endPos, ref body)
        {
            Argname = argname;
        }

        AstCatch(string? source, Position startPos, Position endPos, AstNode argname) : base(source, startPos, endPos)
        {
            Argname = argname;
        }

        public override AstNode ShallowClone()
        {
            var res = new AstCatch(Source, Start, End, Argname);
            res.Body.AddRange(Body.AsReadOnlySpan());
            return res;
        }

        public override void Visit(TreeWalker w)
        {
            base.Visit(w);
            w.Walk(Argname);
        }

        public override void Transform(TreeTransformer tt)
        {
            base.Transform(tt);
            Argname = tt.Transform(Argname)!;
        }

        public override void CodeGen(OutputContext output)
        {
            output.Print("catch");
            if (Argname != null)
            {
                output.Space();
                Argname.Print(output, true);
            }

            output.Space();
            output.PrintBraced(this, false);
        }
    }
}
