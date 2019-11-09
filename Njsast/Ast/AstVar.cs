using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// A `var` statement
    public class AstVar : AstDefinitions
    {
        public AstVar(Parser parser, Position startPos, Position endPos, ref StructList<AstVarDef> definitions) : base(
            parser, startPos, endPos, ref definitions)
        {
        }

        public AstVar(AstNode from) : base(from)
        {
        }

        public AstVar(ref StructList<AstVarDef> definitions) : base(ref definitions)
        {
        }

        public override void CodeGen(OutputContext output)
        {
            DoPrint(output, "var");
        }
    }
}
