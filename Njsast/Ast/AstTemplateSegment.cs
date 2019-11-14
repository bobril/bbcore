using System;
using Njsast.AstDump;
using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// A segment of a template string literal
    public class AstTemplateSegment : AstNode
    {
        /// Content of the segment
        public string Value;

        /// Raw content of the segment
        public string Raw;

        public AstTemplateSegment(string? source, Position startLoc, Position endLoc, string value, string raw) : base(source, startLoc, endLoc)
        {
            Value = value;
            Raw = raw;
        }

        public override void DumpScalars(IAstDumpWriter writer)
        {
            base.DumpScalars(writer);
            writer.PrintProp("Value", Value);
            writer.PrintProp("Raw", Raw);
        }

        public override AstNode ShallowClone()
        {
            return new AstTemplateSegment(Source, Start, End, Value, Raw);
        }

        public override void CodeGen(OutputContext output)
        {
            throw new InvalidOperationException();
        }
    }
}
