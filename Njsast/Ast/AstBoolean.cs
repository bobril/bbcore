using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// Base class for booleans
    public abstract class AstBoolean : AstAtom
    {
        public AstBoolean(string? source, Position startLoc, Position endLoc) : base(source, startLoc, endLoc)
        {
        }

        public override bool NeedParens(OutputContext output)
        {
            if (output.Options.ShortenBooleans)
            {
                var p = output.Parent();
                return p is AstPropAccess propAccess && propAccess.Expression == this
                       || p is AstCall call && call.Expression == this
                       || p is AstBinary binary
                       && binary.Operator == Operator.Power
                       && binary.Left == this;
            }

            return false;
        }
    }
}
