using Njsast.Reader;

namespace Njsast.Ast
{
    /// A symbol exported from this module, but it is used in the other module, and its real name is irrelevant for this module's purposes
    public class AstSymbolExportForeign : AstSymbol
    {
        public AstSymbolExportForeign(AstSymbol symbol) : base(symbol)
        {
        }

        public AstSymbolExportForeign(string? source, Position startPos, Position endPos, string name) : base(source, startPos, endPos, name)
        {
        }

        public override AstNode ShallowClone()
        {
            return new AstSymbolExportForeign(Source, Start, End, Name);
        }
    }
}
