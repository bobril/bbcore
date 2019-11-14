using Njsast.Reader;

namespace Njsast.Ast
{
    /// Symbol defining a function
    public class AstSymbolDefun : AstSymbolDeclaration
    {
        public AstSymbolDefun(AstSymbol from) : base(from)
        {
        }

        AstSymbolDefun(string? source, Position startLoc, Position endLoc, string name, AstNode? init) :
            base(source, startLoc, endLoc, name, init)
        {
        }

        public override AstNode ShallowClone()
        {
            return new AstSymbolDefun(Source, Start, End, Name, Init);
        }
    }
}
