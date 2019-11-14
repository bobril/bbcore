using Njsast.Reader;

namespace Njsast.Ast
{
    /// A constant declaration
    public class AstSymbolConst : AstSymbolBlockDeclaration
    {
        AstSymbolConst(string? source, Position startLoc, Position endLoc, string name, AstNode? init) :
            base(source, startLoc, endLoc, name, init)
        {
        }

        public AstSymbolConst(AstSymbol name) : base(name)
        {
        }

        public override AstNode ShallowClone()
        {
            return new AstSymbolConst(Source, Start, End, Name, Init);
        }
    }
}
