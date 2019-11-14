using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// A reference to new.target
    public class AstNewTarget : AstNode
    {
        public AstNewTarget(string? source, Position startLoc, Position endLoc) : base(source, startLoc, endLoc)
        {
        }

        public override AstNode ShallowClone()
        {
            return new AstNewTarget(Source, Start, End);
        }

        public override void CodeGen(OutputContext output)
        {
            output.Print("new.target");
        }
    }
}
