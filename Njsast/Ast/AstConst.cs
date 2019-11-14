using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// A `const` statement
    public class AstConst : AstDefinitions
    {
        public AstConst(string? source, Position startPos, Position endPos, ref StructList<AstVarDef> definitions) :
            base(source, startPos, endPos, ref definitions)
        {
        }

        AstConst(string? source, Position startPos, Position endPos) : base(source, startPos, endPos)
        {
        }

        public override AstNode ShallowClone()
        {
            var res = new AstConst(Source, Start, End);
            res.Definitions.AddRange(Definitions.AsReadOnlySpan());
            return res;
        }

        public override void CodeGen(OutputContext output)
        {
            DoPrint(output, "const");
        }
    }
}
