using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// The `super` symbol
    public class AstSuper : AstThis
    {
        public AstSuper(string? source, Position startLoc, Position endLoc) : base(source, startLoc, endLoc, "super")
        {
        }

        public override void CodeGen(OutputContext output)
        {
            output.Print("super");
        }

        public override AstNode ShallowClone()
        {
            return new AstSuper(Source, Start, End);
        }
    }
}
