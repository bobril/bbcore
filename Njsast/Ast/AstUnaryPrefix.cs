using Njsast.ConstEval;
using Njsast.Output;
using Njsast.Reader;
using Njsast.Runtime;

namespace Njsast.Ast
{
    /// Unary prefix expression, i.e. `typeof i` or `++i`
    public class AstUnaryPrefix : AstUnary
    {
        public AstUnaryPrefix(Parser parser, Position startLoc, Position endLoc, Operator @operator, AstNode expression)
            : base(parser, startLoc, endLoc, @operator, expression)
        {
        }

        public AstUnaryPrefix(Operator @operator, AstNode expression) : base(@operator, expression)
        {
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

        public override bool IsConstValue(IConstEvalCtx ctx = null)
        {
            if (!Expression.IsConstValue(ctx)) return false;
            if (Operator == Operator.Void) return true;
            if (Operator == Operator.Addition) return true;
            if (Operator == Operator.Subtraction) return true;
            if (Operator == Operator.LogicalNot) return true;
            if (Operator == Operator.BitwiseNot) return true;
            return false;
        }

        public override object ConstValue(IConstEvalCtx ctx = null)
        {
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