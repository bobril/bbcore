using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// Unary postfix expression, i.e. `i++`
    public class AstUnaryPostfix : AstUnary
    {
        public AstUnaryPostfix(string? source, Position startLoc, Position endLoc, Operator @operator,
            AstNode expression) : base(source, startLoc, endLoc, @operator, expression)
        {
        }

        public override AstNode ShallowClone()
        {
            return new AstUnaryPostfix(Source, Start, End, Operator, Expression);
        }

        public override void CodeGen(OutputContext output)
        {
            Expression.Print(output);
            output.Print(Operator);
        }
    }
}
