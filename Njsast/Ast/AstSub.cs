using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// Index-style property access, i.e. `a["foo"]`
    public class AstSub : AstPropAccess
    {
        public AstSub(Parser parser, Position startLoc, Position endLoc, AstNode expression, AstNode property) : base(
            parser, startLoc, endLoc, expression, property)
        {
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
