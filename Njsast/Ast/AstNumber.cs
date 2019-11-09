using System.Globalization;
using Njsast.AstDump;
using Njsast.ConstEval;
using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// A number literal
    public class AstNumber : AstConstant
    {
        /// [number] the numeric value
        public readonly double Value;

        /// [string] numeric value as string (optional)
        public readonly string? Literal;

        public AstNumber(Parser parser, Position startLoc, Position endLoc, double value, string literal) : base(parser,
            startLoc, endLoc)
        {
            Value = value;
            Literal = literal;
        }

        public AstNumber(double value)
        {
            Value = value;
            Literal = null;
        }

        public override void DumpScalars(IAstDumpWriter writer)
        {
            base.DumpScalars(writer);
            writer.PrintProp("Value", Value.ToString(CultureInfo.InvariantCulture));
            writer.PrintProp("Literal", Literal);
        }

        public override void CodeGen(OutputContext output)
        {
            output.PrintNumber(Value);
        }

        public override bool NeedParens(OutputContext output)
        {
            var p = output.Parent();
            if (p is AstPropAccess propAccess && propAccess.Expression == this)
            {
                if (Value < 0)
                {
                    return true;
                }
            }

            return false;
        }

        public override object? ConstValue(IConstEvalCtx? ctx = null)
        {
            return Value;
        }
    }
}
