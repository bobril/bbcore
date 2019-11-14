using Njsast.Reader;

namespace Njsast.Ast
{
    /// Symbol naming the exception in catch
    public class AstSymbolCatch : AstSymbolBlockDeclaration
    {
        AstSymbolCatch(string? source, Position startLoc, Position endLoc, string name, AstNode? init): base(source, startLoc, endLoc, name, init)
        {
        }

        public AstSymbolCatch(AstSymbol name) : base(name)
        {
        }

        public override AstNode ShallowClone()
        {
            return new AstSymbolCatch(Source, Start, End, Name, Init);
        }
    }
}
