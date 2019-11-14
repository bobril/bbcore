using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// A `let` statement
    public class AstLet : AstDefinitions
    {
        public AstLet(string? source, Position startPos, Position endPos, ref StructList<AstVarDef> definitions) : base(source, startPos, endPos, ref definitions)
        {
        }

        AstLet(string? source, Position startPos, Position endPos) : base(source, startPos, endPos)
        {
        }

        public override AstNode ShallowClone()
        {
            var res = new AstLet(Source, Start, End);
            res.Definitions.AddRange(Definitions.AsReadOnlySpan());
            return res;
        }

        public override void CodeGen(OutputContext output)
        {
            DoPrint(output, "let");
        }
    }
}
