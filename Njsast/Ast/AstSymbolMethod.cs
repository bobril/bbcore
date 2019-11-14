using Njsast.Reader;

namespace Njsast.Ast
{
    /// Symbol in an object defining a method
    public class AstSymbolMethod : AstSymbol
    {
        public AstSymbolMethod(string? source, Position startLoc, Position endLoc, string name) : base(source, startLoc, endLoc, name)
        {
        }

        public override AstNode ShallowClone()
        {
            return new AstSymbolMethod(Source, Start, End, Name);
        }
    }
}
