using Njsast.Reader;

namespace Njsast.Ast
{
    /// A block-scoped `let` declaration
    public class AstSymbolLet : AstSymbolBlockDeclaration
    {
        public AstSymbolLet(AstSymbol name) : base(name)
        {
        }

        AstSymbolLet(string? source, Position startLoc, Position endLoc, string name, AstNode? init) :
            base(source, startLoc, endLoc, name, init)
        {
        }

        public override AstNode ShallowClone()
        {
            return new AstSymbolLet(Source, Start, End, Name, Init);
        }
    }
}
