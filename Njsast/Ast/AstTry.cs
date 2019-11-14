using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// A `try` statement
    public class AstTry : AstBlock
    {
        /// [AstCatch?] the catch block, or null if not present
        public AstCatch? Bcatch;

        /// [AstFinally?] the finally block, or null if not present
        public AstFinally? Bfinally;

        public AstTry(string? source, Position startPos, Position endPos, ref StructList<AstNode> body, AstCatch? bcatch,
            AstFinally? bfinally) : base(source, startPos, endPos, ref body)
        {
            Bcatch = bcatch;
            Bfinally = bfinally;
        }

        AstTry(string? source, Position startPos, Position endPos, AstCatch? bcatch, AstFinally? bfinally) : base(source, startPos, endPos)
        {
            Bcatch = bcatch;
            Bfinally = bfinally;
        }

        public override AstNode ShallowClone()
        {
            var res = new AstTry(Source, Start, End, Bcatch, Bfinally);
            res.Body.AddRange(Body.AsReadOnlySpan());
            return res;
        }

        public override void Visit(TreeWalker w)
        {
            base.Visit(w);
            w.Walk(Bcatch);
            w.Walk(Bfinally);
        }

        public override void Transform(TreeTransformer tt)
        {
            base.Transform(tt);
            if (Bcatch != null)
                Bcatch = (AstCatch) tt.Transform(Bcatch);
            if (Bfinally != null)
                Bfinally = (AstFinally) tt.Transform(Bfinally);
        }

        public override void CodeGen(OutputContext output)
        {
            output.Print("try");
            output.Space();
            output.PrintBraced(Body, false);
            if (Bcatch != null)
            {
                output.Space();
                Bcatch.Print(output);
            }

            if (Bfinally != null)
            {
                output.Space();
                Bfinally.Print(output);
            }
        }
    }
}
