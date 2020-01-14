using Njsast.ConstEval;
using Njsast.Output;
using Njsast.Reader;
using Njsast.Runtime;

namespace Njsast.Ast
{
    /// Unary prefix expression, i.e. `typeof i` or `++i`
    public class AstUnaryPrefix : AstUnary
    {
        public AstUnaryPrefix(string? source, Position startLoc, Position endLoc, Operator @operator, AstNode expression)
            : base(source, startLoc, endLoc, @operator, expression)
        {
        }

        public AstUnaryPrefix(Operator @operator, AstNode expression) : base(@operator, expression)
        {
        }

        public override AstNode ShallowClone()
        {
            return new AstUnaryPrefix(Source, Start, End, Operator, Expression);
        }

        public override void CodeGen(OutputContext output)
        {
            output.Print(Operator);
            if (OutputContext.OperatorStartsWithLetter(Operator)
                || OutputContext.OperatorEndsWithPlusOrMinus(Operator)
                && Expression is AstUnaryPrefix nestedUnary
                && OutputContext.OperatorStartsWithPlusOrMinus(nestedUnary.Operator))
            {
                output.Space();
            }

            Expression.Print(output);
        }

        public override object? ConstValue(IConstEvalCtx? ctx = null)
        {
            if (Operator == Operator.TypeOf)
                return null;
            var v = Expression.ConstValue(ctx?.StripPathResolver());
            if (v == null) return null;
            if (Operator == Operator.Void) return AstUndefined.Instance;
            if (Operator == Operator.Addition) return v is double ? v : TypeConverter.ToNumber(v);
            if (Operator == Operator.Subtraction) return v is double d ? -d : -TypeConverter.ToNumber(v);
            if (Operator == Operator.LogicalNot)
                return TypeConverter.ToBoolean(v) ? AstFalse.BoxedFalse : AstTrue.BoxedTrue;
            if (Operator == Operator.BitwiseNot)
                return ~TypeConverter.ToInt32(v);
            return null;
        }
    }
}
