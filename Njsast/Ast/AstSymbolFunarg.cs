using Njsast.Reader;

namespace Njsast.Ast
{
    /// Symbol naming a function argument
    public class AstSymbolFunarg : AstSymbolVar
    {
        public AstSymbolFunarg(AstSymbol from) : base(from)
        {
        }

        public AstSymbolFunarg(Position start, Position end, string name) : base(start, end, name)
        {
        }
    }
}