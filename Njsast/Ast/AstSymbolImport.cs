using Njsast.Reader;

namespace Njsast.Ast
{
    /// Symbol referring to an imported name
    public class AstSymbolImport : AstSymbolBlockDeclaration
    {
        AstSymbolImport(string? source, Position startLoc, Position endLoc, string name, AstNode? init) :
            base(source, startLoc, endLoc, name, init)
        {
        }

        public AstSymbolImport(AstSymbol init) : base(init)
        {
        }

        public override AstNode ShallowClone()
        {
            return new AstSymbolImport(Source, Start, End, Name, Init);
        }
    }
}
