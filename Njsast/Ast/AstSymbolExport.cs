using Njsast.Reader;

namespace Njsast.Ast
{
    /// Symbol referring to a name to export
    public class AstSymbolExport : AstSymbolRef
    {
        public AstSymbolExport(AstSymbol symbol) : base(symbol)
        {
        }

        public AstSymbolExport(string? source, Position startPos, Position endPos, string name) : base(source, startPos, endPos, name)
        {
        }
    }
}
