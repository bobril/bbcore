using Njsast.Reader;

namespace Njsast.Ast
{
    /// A symbol imported from a module, but it is defined in the other module, and its real name is irrelevant for this module's purposes
    public class AstSymbolImportForeign : AstSymbol
    {
        public AstSymbolImportForeign(Parser parser, Position startLoc, Position endLoc, string name) : base(parser, startLoc, endLoc, name)
        {
        }

        public AstSymbolImportForeign(AstSymbol symbol) : base(symbol)
        {
        }
    }
}
