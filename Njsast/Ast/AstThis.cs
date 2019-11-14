using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// The `this` symbol or `super` as AstSuper
    public class AstThis : AstSymbol
    {
        public AstThis(string? source, Position startLoc, Position endLoc) : base(source, startLoc, endLoc, "this")
        {
        }

        protected AstThis(string? source, Position startLoc, Position endLoc, string super) : base(source, startLoc, endLoc, super)
        {
        }

        public override AstNode ShallowClone()
        {
            return new AstThis(Source, Start, End);
        }

        public override void CodeGen(OutputContext output)
        {
            output.Print("this");
        }
    }
}
