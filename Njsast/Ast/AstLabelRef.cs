using Njsast.Reader;

namespace Njsast.Ast
{
    /// Reference to a label symbol
    public class AstLabelRef : AstSymbol
    {
        public new AstLabel? Thedef;

        public AstLabelRef(string? source, Position startLoc, Position endLoc, string name) : base(source, startLoc, endLoc, name)
        {
        }

        public AstLabelRef(AstSymbol symbol) : base(symbol)
        {
        }

        public override AstNode ShallowClone()
        {
            return new AstLabelRef(Source, Start, End, Name);
        }
    }
}
