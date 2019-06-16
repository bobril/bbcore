using Njsast.AstDump;
using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// Symbol naming a label (declaration)
    public class AstLabel : AstSymbol
    {
        /// [AstLoopControl*] a list of nodes referring to this label
        public StructList<AstLoopControl> References;

        public bool IsLoop;

        public string MangledName;

        public AstLabel(Parser parser, Position startLoc, Position endLoc, string name) : base(parser, startLoc, endLoc,
            name)
        {
        }

        public override void DumpScalars(IAstDumpWriter writer)
        {
            base.DumpScalars(writer);
            writer.PrintProp("IsLoop", IsLoop);
        }

        public override void CodeGen(OutputContext output)
        {
            output.PrintName(MangledName ?? Name);
        }
    }
}