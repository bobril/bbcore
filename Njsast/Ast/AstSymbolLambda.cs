using Njsast.Reader;

namespace Njsast.Ast
{
    /// Symbol naming a function expression
    public class AstSymbolLambda : AstSymbolDeclaration
    {
        public AstSymbolLambda(string? source, Position startLoc, Position endLoc, string name, AstNode? init) : base(source, startLoc, endLoc, name, init)
        {
        }

        public AstSymbolLambda(AstSymbol from) : base(from)
        {
        }

        public override AstNode ShallowClone()
        {
            return new AstSymbolLambda(Source, Start, End, Name, Init);
        }
    }
}
