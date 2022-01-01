using System.Globalization;
using System.Numerics;
using Njsast.AstDump;
using Njsast.ConstEval;
using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast;

/// A bigint literal
public class AstBigInt : AstConstant
{
    /// the numeric value
    public readonly BigInteger Value;

    public AstBigInt(string? source, Position startLoc, Position endLoc, BigInteger value) : base(source, startLoc, endLoc)
    {
        Value = value;
    }

    public AstBigInt(BigInteger value)
    {
        Value = value;
    }

    public override void DumpScalars(IAstDumpWriter writer)
    {
        base.DumpScalars(writer);
        writer.PrintProp("Value", Value.ToString(CultureInfo.InvariantCulture));
    }

    public override AstNode ShallowClone()
    {
        return new AstBigInt(Source, Start, End, Value);
    }

    public override void CodeGen(OutputContext output)
    {
        output.PrintBigInt(Value);
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

    public override object ConstValue(IConstEvalCtx? ctx = null)
    {
        return Value;
    }
}
