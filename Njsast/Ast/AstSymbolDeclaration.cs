using Njsast.Reader;

namespace Njsast.Ast
{
    /// A declaration symbol (symbol in var/const, function name or argument, symbol in catch)
    public abstract class AstSymbolDeclaration : AstSymbol
    {
        public AstNode? Init;

        protected AstSymbolDeclaration(string? source, Position startLoc, Position endLoc, string name, AstNode? init) :
            base(source, startLoc, endLoc, name)
        {
            Init = init;
        }

        protected AstSymbolDeclaration(AstSymbol symbol, AstNode? init = null) : base(symbol)
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
