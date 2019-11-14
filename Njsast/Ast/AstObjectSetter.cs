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

        public AstObjectSetter(string? source, Position startLoc, Position endLoc, AstNode key, AstNode value,
            bool @static) : base(source, startLoc, endLoc, key, value)
        {
            Static = @static;
        }

        public override void DumpScalars(IAstDumpWriter writer)
        {
            base.DumpScalars(writer);
            writer.PrintProp("Static", Static);
        }

        public override AstNode ShallowClone()
        {
            return new AstObjectSetter(Source, Start, End, Key, Value, Static);
        }

        public override void CodeGen(OutputContext output)
        {
            PrintGetterSetter(output, "set", Static);
        }
    }
}
