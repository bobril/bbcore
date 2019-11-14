using Njsast.Reader;

namespace Njsast.Ast
{
    /// Symbol naming a class's name in a class declaration. Lexically scoped to its containing scope, and accessible within the class.
    public class AstSymbolDefClass : AstSymbolBlockDeclaration
    {
        AstSymbolDefClass(string? source, Position startLoc, Position endLoc, string name, AstNode? init) :
            base(source, startLoc, endLoc, name, init)
        {
        }

        public AstSymbolDefClass(AstSymbol name, AstNode? init = null) : base(name, init)
        {
        }

        public override AstNode ShallowClone()
        {
            return new AstSymbolDefClass(Source, Start, End, Name, Init);
        }
    }
}
