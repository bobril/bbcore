using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// A dotted property access expression
    public class AstDot : AstPropAccess
    {
        public AstDot(string? source, Position startLoc, Position endLoc, AstNode expression, string property) : base(
            source, startLoc, endLoc, expression, property)
        {
        }

        public AstDot(AstNode expression, string propName) : base(expression, propName)
        {
        }

        public override AstNode ShallowClone()
        {
            return new AstDot(Source, Start, End, Expression, (string)Property);
        }

        public override void CodeGen(OutputContext output)
        {
            Expression.Print(output, Expression is AstBinary && output.NeedNodeParens(Expression));
            if (output.NeedDotAfterNumber())
            {
                output.Print(".");
            }

            output.AddMapping(Expression.Source, Expression.End, false);
            output.Print(".");
            output.Print((string) Property);
        }
    }
}
