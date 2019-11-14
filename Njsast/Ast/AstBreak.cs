using Njsast.Reader;

namespace Njsast.Ast
{
    /// A `break` statement
    public class AstBreak : AstLoopControl
    {
        public AstBreak(string? source, Position startPos, Position endPos, AstLabelRef? label = null) : base(source, startPos, endPos, label)
        {
        }

        public override AstNode ShallowClone()
        {
            return new AstBreak(Source, Start, End, Label);
        }
    }
}
