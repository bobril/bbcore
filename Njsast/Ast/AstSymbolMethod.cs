using Njsast.Reader;

namespace Njsast.Ast
{
    /// Symbol in an object defining a method
    public class AstSymbolMethod : AstSymbol
    {
        public AstSymbolMethod(Parser parser, Position startLoc, Position endLoc, string name) : base(parser, startLoc, endLoc, name)
        {
        }
    }
}
