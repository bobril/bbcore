using Njsast.Reader;

namespace Njsast.Ast
{
    /// Symbol naming a class's name. Lexically scoped to the class.
    public class AstSymbolClass : AstSymbolDeclaration
    {
        public AstSymbolClass(Parser parser, Position startLoc, Position endLoc, string name, AstNode init) : base(parser, startLoc, endLoc, name, init)
        {
        }
    }
}
