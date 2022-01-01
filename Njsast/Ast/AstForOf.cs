using Njsast.Reader;

namespace Njsast.Ast;

/// A `for ... of` statement
public class AstForOf : AstForIn
{
    public AstForOf(string? source, Position startPos, Position endPos, AstStatement body, AstNode init, AstNode @object, bool await) : base(source, startPos, endPos, body, init, @object, await)
    {
    }

    public override AstNode ShallowClone()
    {
        return new AstForOf(Source, Start, End, Body, Init, Object, Await);
    }
}
