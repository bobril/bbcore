using Njsast.Reader;

namespace Njsast.Ast
{
    /// A `continue` statement
    public class AstContinue : AstLoopControl
    {
        public AstContinue(string? source, Position startPos, Position endPos, AstLabelRef? label = null) : base(source,
            startPos, endPos, label)
        {
        }

        public override AstNode ShallowClone()
        {
            return new AstContinue(Source, Start, End, Label);
        }
    }
}
