using Njsast.AstDump;
using Njsast.ConstEval;
using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// A string literal
    public class AstString : AstConstant
    {
        /// [string] the contents of this string
        public readonly string Value;

        public AstString(string? source, Position startLoc, Position endLoc, string value) : base(source, startLoc, endLoc)
        {
            Value = value;
        }

        public AstString(string value)
        {
            Value = value;
        }

        public AstString(AstSymbol parseIdent): base(parseIdent.Source,parseIdent.Start,parseIdent.End)
        {
            Value = parseIdent.Name;
        }

        public override void DumpScalars(IAstDumpWriter writer)
        {
            base.DumpScalars(writer);
            writer.PrintProp("Value", Value);
        }

        public override AstNode ShallowClone()
        {
            return new AstString(Source, Start, End, Value);
        }

        public override void CodeGen(OutputContext output)
        {
            output.PrintString(Value);
        }

        public override object? ConstValue(IConstEvalCtx? ctx = null)
        {
            if (ctx != null) return ctx.ConstStringResolver(Value);
            return Value;
        }
    }
}
