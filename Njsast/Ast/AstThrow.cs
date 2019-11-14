using Njsast.Reader;

namespace Njsast.Ast
{
    /// A `throw` statement
    public class AstThrow : AstExit
    {
        public AstThrow(string? source, Position startPos, Position endPos, AstNode value) : base(source, startPos, endPos, value)
        {
        }

        public override AstNode ShallowClone()
        {
            return new AstThrow(Source, Start, End, Value!);
        }
    }
}
