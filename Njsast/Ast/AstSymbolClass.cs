namespace Njsast.Ast
{
    /// Symbol naming a class's name. Lexically scoped to the class.
    public class AstSymbolClass : AstSymbolDeclaration
    {
        public AstSymbolClass(AstSymbol from) : base(from)
        {
        }

        public override AstNode ShallowClone()
        {
            return new AstSymbolClass(this);
        }
    }
}
