using Njsast.Reader;

namespace Njsast.Ast
{
    /// Symbol defining a variable
    public class AstSymbolVar : AstSymbolDeclaration
    {
        public AstSymbolVar(AstSymbol name) : base(name, null)
        {
        }

        public AstSymbolVar(Position start, Position end, string name) : base(start, end, name)
        {
        }
    }
}