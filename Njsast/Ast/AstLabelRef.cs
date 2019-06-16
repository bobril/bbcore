using Njsast.Reader;

namespace Njsast.Ast
{
    /// Reference to a label symbol
    public class AstLabelRef : AstSymbol
    {
        public new AstLabel Thedef;

        public AstLabelRef(Parser parser, Position startLoc, Position endLoc, string name) : base(parser, startLoc, endLoc, name)
        {
        }

        public AstLabelRef(AstSymbol symbol) : base(symbol)
        {
        }
    }
}
