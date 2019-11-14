using Njsast.Reader;

namespace Njsast.Ast
{
    /// A `return` statement
    public class AstReturn : AstExit
    {
        public AstReturn(string? source, Position startPos, Position endPos, AstNode? value) : base(source, startPos, endPos, value)
        {
        }

        public AstReturn(AstNode value) : base(value)
        {
        }

        public override AstNode ShallowClone()
        {
            return new AstReturn(Source, Start, End, Value);
        }
    }
}
