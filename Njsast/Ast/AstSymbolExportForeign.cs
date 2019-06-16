using Njsast.Reader;

namespace Njsast.Ast
{
    /// A symbol exported from this module, but it is used in the other module, and its real name is irrelevant for this module's purposes
    public class AstSymbolExportForeign : AstSymbol
    {
        public AstSymbolExportForeign(AstSymbol symbol) : base(symbol)
        {
        }

        public AstSymbolExportForeign(Parser parser, Position startPos, Position endPos, string name) : base(parser, startPos, endPos, name)
        {
        }
    }
}
