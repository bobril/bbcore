namespace Njsast.Ast
{
    /// Base class for block-scoped declaration symbols
    public class AstSymbolBlockDeclaration : AstSymbolDeclaration
    {
        public AstSymbolBlockDeclaration(AstSymbol symbol, AstNode init = null) : base(symbol, init)
        {
        }
    }
}
