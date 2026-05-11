using Njsast.AstDump;
using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast;

public class AstRawStatement : AstStatement
{
    public string Raw;

    public AstRawStatement(string? source, Position startPos, Position endPos, string raw) : base(source, startPos,
        endPos)
    {
        Raw = raw;
    }

    AstRawStatement(string? source, Position startPos, Position endPos) : base(source, startPos, endPos)
    {
        Raw = "";
    }

    public override AstNode ShallowClone()
    {
        return new AstRawStatement(Source, Start, End, Raw);
    }

    public override void CodeGen(OutputContext output)
    {
        output.Print(Raw);
    }

    public override void DumpScalars(IAstDumpWriter writer)
    {
        base.DumpScalars(writer);
        writer.PrintProp("Raw", Raw);
    }
}
