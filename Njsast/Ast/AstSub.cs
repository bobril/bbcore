using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// Index-style property access, i.e. `a["foo"]`
    public class AstSub : AstPropAccess
    {
        public AstSub(string? source, Position startLoc, Position endLoc, AstNode expression, AstNode property) : base(
            source, startLoc, endLoc, expression, property)
        {
        }

        public override AstNode ShallowClone()
        {
            return new AstSub(Source, Start, End, Expression, (AstNode)Property);
        }

        public override void CodeGen(OutputContext output)
        {
            Expression.Print(output);
            output.Print("[");
            ((AstNode) Property).Print(output);
            output.Print("]");
        }
    }
}
