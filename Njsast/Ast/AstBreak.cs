using Njsast.Reader;

namespace Njsast.Ast
{
    /// A `break` statement
    public class AstBreak : AstLoopControl
    {
        public AstBreak(Parser parser, Position startPos, Position endPos, AstLabelRef? label = null) : base(parser, startPos, endPos, label)
        {
        }
    }
}
