using Njsast.AstDump;
using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast;

public class AstJsxText : AstNode
{
    public string Raw;

    public AstJsxText(string? source, Position startLoc, Position endLoc, string raw) : base(source, startLoc, endLoc)
    {
        Raw = raw;
    }

    AstJsxText(string raw)
    {
        Raw = raw;
    }

    public override void DumpScalars(IAstDumpWriter writer)
    {
        base.DumpScalars(writer);
        writer.PrintProp("Raw", Raw);
    }

    public override AstNode ShallowClone()
    {
        return new AstJsxText(Raw) { Source = Source, Start = Start, End = End };
    }

    public override void CodeGen(OutputContext output)
    {
        output.Print(Raw);
    }
}
