using Njsast.AstDump;
using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// A regexp literal
    public class AstRegExp : AstConstant
    {
        /// [RegExp] the actual regexp
        public RegExp Value;

        public AstRegExp(string? source, Position startLoc, Position endLoc, RegExp value) : base(source, startLoc, endLoc)
        {
            Value = value;
        }

        public override void DumpScalars(IAstDumpWriter writer)
        {
            base.DumpScalars(writer);
            writer.PrintProp("Pattern", Value.Pattern);
            writer.PrintProp("Flags", Value.Flags.ToString());
        }

        public override AstNode ShallowClone()
        {
            return new AstRegExp(Source, Start, End, Value);
        }

        public override void CodeGen(OutputContext output)
        {
            output.Print("/");
            output.Print(Value.Pattern);
            output.Print("/");
            var f = Value.Flags;
            if (f.HasFlag(RegExpFlags.GlobalMatch))
                output.Print("g");
            if (f.HasFlag(RegExpFlags.IgnoreCase))
                output.Print("i");
            if (f.HasFlag(RegExpFlags.Multiline))
                output.Print("m");
            if (f.HasFlag(RegExpFlags.Sticky))
                output.Print("y");
            if (f.HasFlag(RegExpFlags.Unicode))
                output.Print("u");
        }
    }
}
