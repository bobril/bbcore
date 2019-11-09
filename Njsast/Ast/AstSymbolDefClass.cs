namespace Njsast.Ast
{
    /// Symbol naming a class's name in a class declaration. Lexically scoped to its containing scope, and accessible within the class.
    public class AstSymbolDefClass : AstSymbolBlockDeclaration
    {
        public AstSymbolDefClass(AstSymbol name, AstNode? init = null) : base(name, init)
        {
        }
    }
}
