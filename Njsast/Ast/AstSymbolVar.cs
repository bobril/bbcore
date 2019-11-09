namespace Njsast.Ast
{
    /// Symbol defining a variable
    public class AstSymbolVar : AstSymbolDeclaration
    {
        public AstSymbolVar(AstSymbol name) : base(name)
        {
        }

        public AstSymbolVar(string name) : base(name)
        {
        }

        public AstSymbolVar(AstNode from, string name) : base(from, name)
        {
        }
    }
}
