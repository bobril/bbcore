using System;
using Njsast.AstDump;
using Njsast.ConstEval;
using Njsast.Output;
using Njsast.Reader;
using Njsast.Runtime;

namespace Njsast.Ast
{
    /// Binary expression, i.e. `a + b`
    public class AstBinary : AstNode
    {
        public Operator Operator;

        /// [AstNode] left-hand side expression
        public AstNode Left;

        /// [AstNode] right-hand side expression
        public AstNode Right;

        public AstBinary(string? source, Position startLoc, Position endLoc, AstNode left, AstNode right, Operator op) :
            base(source, startLoc, endLoc)
        {
            Left = left;
            Right = right;
            Operator = op;
        }

        protected AstBinary(AstNode left, AstNode right, Operator op)
        {
            Left = left;
            Right = right;
            Operator = op;
        }

        public override void Visit(TreeWalker w)
        {
            base.Visit(w);
            w.Walk(Left);
            w.Walk(Right);
        }

        public override void Transform(TreeTransformer tt)
        {
            base.Transform(tt);
            Left = tt.Transform(Left)!;
            Right = tt.Transform(Right)!;
        }

        public override void DumpScalars(IAstDumpWriter writer)
        {
            base.DumpScalars(writer);
            writer.PrintProp("Operator", Operator.ToString());
        }

        public override AstNode ShallowClone()
        {
            return new AstBinary(Source, Start, End, Left, Right, Operator);
        }

        public override void CodeGen(OutputContext output)
        {
            var op = Operator;
            Left.Print(output, Left is AstBinary && output.NeedNodeParens(Left));
            if (OutputContext.OperatorToString(op)[0] == '>' /* ">>" ">>>" ">" ">=" */
                && Left is AstUnaryPostfix leftPostfix
                && leftPostfix.Operator == Operator.DecrementPostfix)
            {
                // space is mandatory to avoid outputting -->
                output.Print(" ");
            }
            else
            {
                // the space is optional depending on "beautify"
                output.Space();
            }

            output.Print(op);
            if ((op == Operator.LessThan || op == Operator.LeftShift)
                && Right is AstUnaryPrefix rightPrefix
                && rightPrefix.Operator == Operator.LogicalNot
                && rightPrefix.Expression is AstUnaryPrefix rightPrefixPrefix
                && rightPrefixPrefix.Operator == Operator.Decrement)
            {
                // space is mandatory to avoid outputting <!--
                output.Print(" ");
            }
            else
            {
                // the space is optional depending on "beautify"
                output.Space();
            }

            Right.Print(output, Right is AstBinary && output.NeedNodeParens(Right));
        }

        public override bool NeedParens(OutputContext output)
        {
            var p = output.Parent();
            // (foo && bar)()
            if (p is AstCall call && call.Expression == this) return true;
            // typeof (foo && bar)
            if (p is AstUnary) return true;
            // (foo && bar)["prop"], (foo && bar).prop
            if (p is AstPropAccess propAccess && propAccess.Expression == this) return true;
            // this deals with precedence: 3 * (2 + 1)
            if (p is AstBinary binary)
            {
                var po = binary.Operator;
                var pp = OutputContext.Precedence(po);
                var sp = OutputContext.Precedence(Operator);
                if (pp > sp
                    || (pp == sp
                        && (this == binary.Right || po == Operator.Power)))
                {
                    return true;
                }
            }

            return false;
        }

        public override object? ConstValue(IConstEvalCtx? ctx = null)
        {
            var left = Left.ConstValue(ctx);
            if (left == null) return null;
            if (Operator == Operator.LogicalOr) // Short circuit ||
            {
                if (TypeConverter.ToBoolean(left)) return left;
            }

            if (Operator == Operator.LogicalAnd) // Short circuit &&
            {
                if (!TypeConverter.ToBoolean(left)) return left;
            }

            var right = Right.ConstValue(ctx?.StripPathResolver());
            if (right == null) return null;
            switch (Operator)
            {
                case Operator.LogicalOr: // we know that left is false
                case Operator.LogicalAnd: // we know that left is true
                    return right;
                case Operator.BitwiseOr:
                    return TypeConverter.ToInt32(left) | TypeConverter.ToInt32(right);
                case Operator.BitwiseAnd:
                    return TypeConverter.ToInt32(left) & TypeConverter.ToInt32(right);
                case Operator.BitwiseXOr:
                    return TypeConverter.ToInt32(left) ^ TypeConverter.ToInt32(right);
                case Operator.Equals:
                    return JsEquals(left, right) ? AstTrue.BoxedTrue : AstFalse.BoxedFalse;
                case Operator.NotEquals:
                    return JsEquals(left, right) ? AstFalse.BoxedFalse : AstTrue.BoxedTrue;
                case Operator.StrictEquals:
                    return JsStrictEquals(left, right) ? AstTrue.BoxedTrue : AstFalse.BoxedFalse;
                case Operator.StrictNotEquals:
                    return JsStrictEquals(left, right) ? AstFalse.BoxedFalse : AstTrue.BoxedTrue;
                case Operator.Addition:
                    left = TypeConverter.ToPrimitive(left);
                    right = TypeConverter.ToPrimitive(right);
                    if (left is string || right is string)
                    {
                        return TypeConverter.ToString(left) + TypeConverter.ToString(right);
                    }

                    return TypeConverter.ToNumber(left) + TypeConverter.ToNumber(right);
                case Operator.Subtraction:
                    return TypeConverter.ToNumber(left) - TypeConverter.ToNumber(right);
                case Operator.Multiplication:
                    return TypeConverter.ToNumber(left) * TypeConverter.ToNumber(right);
                case Operator.Division:
                    try
                    {
                        return TypeConverter.ToNumber(left) / TypeConverter.ToNumber(right);
                    }
                    catch
                    {
                        return AstNaN.Instance;
                    }
                case Operator.Modulus:
                    try
                    {
                        return TypeConverter.ToNumber(left) % TypeConverter.ToNumber(right);
                    }
                    catch
                    {
                        return AstNaN.Instance;
                    }
                case Operator.Power:
                    return Math.Pow(TypeConverter.ToNumber(left), TypeConverter.ToNumber(right));
                case Operator.LessThan:
                {
                    left = TypeConverter.ToPrimitive(left);
                    right = TypeConverter.ToPrimitive(right);
                    var res = LessThan(left, right);
                    if (res == AstUndefined.Instance) res = AstFalse.BoxedFalse;
                    return res;
                }
                case Operator.GreaterThan:
                {
                    left = TypeConverter.ToPrimitive(left);
                    right = TypeConverter.ToPrimitive(right);
                    var res = LessThan(right, left);
                    if (res == AstUndefined.Instance) res = AstFalse.BoxedFalse;
                    return res;
                }
                case Operator.LessEquals:
                {
                    left = TypeConverter.ToPrimitive(left);
                    right = TypeConverter.ToPrimitive(right);
                    var res = LessThan(right, left);
                    return res == AstFalse.BoxedFalse ? AstTrue.BoxedTrue : AstFalse.BoxedFalse;
                }
                case Operator.GreaterEquals:
                {
                    left = TypeConverter.ToPrimitive(left);
                    right = TypeConverter.ToPrimitive(right);
                    var res = LessThan(left, right);
                    return res == AstFalse.BoxedFalse ? AstTrue.BoxedTrue : AstFalse.BoxedFalse;
                }
                case Operator.LeftShift:
                {
                    var leftNum = TypeConverter.ToInt32(left);
                    var rightNum = TypeConverter.ToUint32(right);
                    return leftNum << (int)(rightNum & 0x1f);
                }
                case Operator.RightShift:
                {
                    var leftNum = TypeConverter.ToInt32(left);
                    var rightNum = TypeConverter.ToUint32(right);
                    return leftNum >> (int)(rightNum & 0x1f);
                }
                case Operator.RightShiftUnsigned:
                {
                    var leftNum = TypeConverter.ToUint32(left);
                    var rightNum = TypeConverter.ToUint32(right);
                    return leftNum >> (int)(rightNum & 0x1f);
                }
            }

            return null;
        }

        static object LessThan(object? left, object? right)
        {
            if (left is string leftStr && right is string rightStr)
            {
                return (string.Compare(leftStr, rightStr, StringComparison.Ordinal) < 0)
                    ? AstTrue.BoxedTrue
                    : AstFalse.BoxedFalse;
            }

            var leftNumber = TypeConverter.ToNumber(left);
            var rightNumber = TypeConverter.ToNumber(right);
            if (double.IsNaN(leftNumber)) return AstUndefined.Instance;
            if (double.IsNaN(rightNumber)) return AstUndefined.Instance;
            //if (leftNumber == rightNumber) return AstFalse.BoxedFalse;
            //if (double.IsPositiveInfinity(leftNumber)) return AstFalse.BoxedFalse;
            //if (double.IsPositiveInfinity(rightNumber)) return AstTrue.BoxedTrue;
            //if (double.IsNegativeInfinity(rightNumber)) return AstFalse.BoxedFalse;
            //if (double.IsNegativeInfinity(leftNumber)) return AstTrue.BoxedTrue;
            return leftNumber < rightNumber ? AstTrue.BoxedTrue : AstFalse.BoxedFalse;
        }

        static bool JsStrictEquals(object left, object right)
        {
            var leftType = TypeConverter.GetJsType(left);
            var rightType = TypeConverter.GetJsType(right);
            if (leftType != rightType)
                return false;
            if (leftType == JsType.Undefined || leftType == JsType.Null)
                return true;
            if (leftType == JsType.Number)
            {
                var leftN = TypeConverter.ToNumber(left);
                var rightN = TypeConverter.ToNumber(right);
                return JsEquals(leftN, rightN);
            }

            if (leftType == JsType.String)
            {
                return TypeConverter.ToString(left) == TypeConverter.ToString(right);
            }

            if (leftType == JsType.Boolean)
            {
                return TypeConverter.ToBoolean(left) == TypeConverter.ToBoolean(right);
            }

            // Return true if x and y refer to the same object. Otherwise, return false. We cannot compare references for now
            return false;
        }

        static bool JsEquals(object left, object right)
        {
            var leftType = TypeConverter.GetJsType(left);
            var rightType = TypeConverter.GetJsType(right);
            if (leftType == rightType)
            {
                if (leftType == JsType.Undefined || leftType == JsType.Null)
                    return true;
                if (leftType == JsType.Number)
                {
                    var leftN = TypeConverter.ToNumber(left);
                    var rightN = TypeConverter.ToNumber(right);
                    return JsEquals(leftN, rightN);
                }

                if (leftType == JsType.String)
                {
                    return TypeConverter.ToString(left) == TypeConverter.ToString(right);
                }

                if (leftType == JsType.Boolean)
                {
                    return TypeConverter.ToBoolean(left) == TypeConverter.ToBoolean(right);
                }

                // Return true if x and y refer to the same object. Otherwise, return false. We cannot compare references for now
                return false;
            }

            if (leftType == JsType.Null && rightType == JsType.Undefined) return true;
            if (leftType == JsType.Undefined && rightType == JsType.Null) return true;
            if (leftType == JsType.Number && rightType == JsType.String ||
                leftType == JsType.String && rightType == JsType.Number)
            {
                return JsEquals(TypeConverter.ToNumber(left), TypeConverter.ToNumber(right));
            }

            if (leftType == JsType.Boolean)
            {
                return JsEquals(TypeConverter.ToNumber(left), right);
            }

            if (rightType == JsType.Boolean)
            {
                return JsEquals(left, TypeConverter.ToNumber(right));
            }

            // If Type(x) is either String or Number and Type(y) is Object,
            //   return the result of the comparison x == ToPrimitive(y).
            // If Type(x) is Object and Type(y) is either String or Number,
            //   return the result of the comparison ToPrimitive(x) == y.
            return false;
        }

        static bool JsEquals(double leftN, double rightN)
        {
            if (double.IsNaN(leftN) || double.IsNaN(rightN))
                return false;
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            return leftN == rightN;
        }
    }
}
