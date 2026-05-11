using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast;

public class AstTypeScriptOnly : AstStatement
{
    public AstTypeScriptOnly(string? source, Position startPos, Position endPos) : base(source, startPos, endPos)
    {
    }

    public override AstNode ShallowClone()
    {
        return new AstTypeScriptOnly(Source, Start, End);
    }

    public override void CodeGen(OutputContext output)
    {
    }
}
