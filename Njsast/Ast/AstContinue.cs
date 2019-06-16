using Njsast.Reader;

namespace Njsast.Ast
{
    /// A `continue` statement
    public class AstContinue : AstLoopControl
    {
        public AstContinue(Parser parser, Position startPos, Position endPos, AstLabelRef label = null) : base(parser,
            startPos, endPos, label)
        {
        }
    }
}
