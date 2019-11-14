using Njsast.Reader;

namespace Njsast.Ast
{
    /// Base class for block-scoped declaration symbols
    public abstract class AstSymbolBlockDeclaration : AstSymbolDeclaration
    {
        protected AstSymbolBlockDeclaration(string? source, Position startLoc, Position endLoc, string name, AstNode? init) :
            base(source, startLoc, endLoc, name, init)
        {
        }

        protected AstSymbolBlockDeclaration(AstSymbol symbol, AstNode? init = null) : base(symbol, init)
        {
        }
    }
}
