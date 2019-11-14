using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// A `var` statement
    public class AstVar : AstDefinitions
    {
        public AstVar(string? source, Position startPos, Position endPos, ref StructList<AstVarDef> definitions) : base(
            source, startPos, endPos, ref definitions)
        {
        }

        AstVar(string? source, Position startPos, Position endPos) : base(source, startPos, endPos)
        {
        }

        public AstVar(AstNode from) : base(from)
        {
        }

        public AstVar(ref StructList<AstVarDef> definitions) : base(ref definitions)
        {
        }

        public override AstNode ShallowClone()
        {
            var res = new AstVar(Source, Start, End);
            res.Definitions.AddRange(Definitions.AsReadOnlySpan());
            return res;
        }

        public override void CodeGen(OutputContext output)
        {
            DoPrint(output, "var");
        }
    }
}
