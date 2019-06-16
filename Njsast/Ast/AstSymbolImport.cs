namespace Njsast.Ast
{
    /// Symbol referring to an imported name
    public class AstSymbolImport : AstSymbolBlockDeclaration
    {
        public AstSymbolImport(AstSymbol init) : base(init)
        {
        }
    }
}
