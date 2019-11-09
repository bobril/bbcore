using Njsast.ConstEval;

namespace Njsast.Ast
{
    /// Symbol used as key in object { key: ... }
    public class AstSymbolProperty : AstSymbol
    {
        public AstSymbolProperty(AstSymbol symbol) : base(symbol)
        {
        }

        public override object? ConstValue(IConstEvalCtx? ctx = null)
        {
            return Name;
        }
    }
}
