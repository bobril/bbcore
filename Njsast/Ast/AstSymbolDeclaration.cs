using Njsast.Reader;

namespace Njsast.Ast
{
    /// A declaration symbol (symbol in var/const, function name or argument, symbol in catch)
    public class AstSymbolDeclaration : AstSymbol
    {
        public AstNode? Init;

        public AstSymbolDeclaration(Parser parser, Position startLoc, Position endLoc, string name, AstNode? init) :
            base(parser, startLoc, endLoc, name)
        {
            Init = init;
        }

        public AstSymbolDeclaration(AstSymbol symbol, AstNode? init = null) : base(symbol)
        {
            Init = init;
        }

        protected AstSymbolDeclaration(string name, AstNode? init = null) : base(name)
        {
            Init = init;
        }

        protected AstSymbolDeclaration(AstNode from, string name, AstNode? init = null) : base(from, name)
        {
            Init = init;
        }
    }
}
