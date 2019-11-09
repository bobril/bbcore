using Njsast.AstDump;
using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// An object setter property
    public class AstObjectSetter : AstObjectProperty
    {
        /// [boolean] whether this is a static setter (classes only)
        public bool Static;

        public AstObjectSetter(Parser parser, Position startLoc, Position endLoc, AstNode key, AstNode value,
            bool @static) : base(parser, startLoc, endLoc, key, value)
        {
            Static = @static;
        }

        public override void DumpScalars(IAstDumpWriter writer)
        {
            base.DumpScalars(writer);
            writer.PrintProp("Static", Static);
        }

        public override void CodeGen(OutputContext output)
        {
            PrintGetterSetter(output, "set", Static);
        }
    }
}
