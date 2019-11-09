using Njsast.Reader;

namespace Njsast.Ast
{
    /// A function definition
    public class AstDefun : AstLambda
    {
        public AstDefun(Parser parser, Position startPos, Position endPos, AstSymbolDeclaration? name, ref StructList<AstNode> argNames, bool isGenerator, bool async, ref StructList<AstNode> body) : base(parser, startPos, endPos, name, ref argNames, isGenerator, async, ref body)
        {
        }
    }
}
