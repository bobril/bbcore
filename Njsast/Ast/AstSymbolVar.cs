using Njsast.Reader;

namespace Njsast.Ast
{
    /// Symbol defining a variable
    public class AstSymbolVar : AstSymbolDeclaration
    {
        public AstSymbolVar(string? source, Position startLoc, Position endLoc, string name, AstNode? init) : base(source, startLoc, endLoc, name, init)
        {
        }

        public AstSymbolVar(AstSymbol name) : base(name)
        {
        }

        public AstSymbolVar(string name) : base(name)
        {
        }

        public AstSymbolVar(AstNode from, string name) : base(from, name)
        {
        }

        public override AstNode ShallowClone()
        {
            return new AstSymbolVar(Source, Start, End, Name, Init);
        }
    }
}
