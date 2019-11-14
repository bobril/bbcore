using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// A `for ... in` statement
    public class AstForIn : AstIterationStatement
    {
        public bool Await;

        /// [AstNode] the `for/in` initialization code
        public AstNode Init;

        /// [AstNode] the object that we're looping through
        public AstNode Object;

        public AstForIn(string? source, Position startPos, Position endPos, AstStatement body, AstNode init,
            AstNode @object) : base(source, startPos, endPos, body)
        {
            Init = init;
            Object = @object;
        }

        public override void Visit(TreeWalker w)
        {
            w.Walk(Init);
            w.Walk(Object);
            base.Visit(w);
        }

        public override void Transform(TreeTransformer tt)
        {
            Init = tt.Transform(Init);
            Object = tt.Transform(Object);
            base.Transform(tt);
        }

        public override AstNode ShallowClone()
        {
            return new AstForIn(Source, Start, End, Body, Init, Object);
        }

        public override void CodeGen(OutputContext output)
        {
            output.Print("for");
            if (Await)
            {
                output.Space();
                output.Print("await");
            }

            output.Space();
            output.Print("(");
            Init.Print(output);
            output.Space();
            output.Print(this is AstForOf ? "of" : "in");
            output.Space();
            Object.Print(output);
            output.Print(")");
            output.Space();
            output.PrintBody(Body);
        }
    }
}
