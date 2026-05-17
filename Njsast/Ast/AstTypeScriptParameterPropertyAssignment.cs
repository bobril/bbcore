using Njsast.Reader;

namespace Njsast.Ast;

public class AstTypeScriptParameterPropertyAssignment : AstSimpleStatement
{
    public AstTypeScriptParameterPropertyAssignment(string? source, Position startPos, Position endPos, AstNode body)
        : base(source, startPos, endPos, body)
    {
    }

    public override AstNode ShallowClone()
    {
        return new AstTypeScriptParameterPropertyAssignment(Source, Start, End, Body);
    }
}
