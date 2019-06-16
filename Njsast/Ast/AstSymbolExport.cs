using Njsast.Reader;

namespace Njsast.Ast
{
    /// Symbol referring to a name to export
    public class AstSymbolExport : AstSymbolRef
    {
        public AstSymbolExport(AstSymbol symbol) : base(symbol)
        {
        }

        public AstSymbolExport(Parser parser, Position startPos, Position endPos, string name) : base(parser, startPos, endPos, name)
        {
        }
    }
}
