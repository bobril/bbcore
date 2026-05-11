using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast;

/// A static initialization block, e.g. `static { ... }` inside a class
public class AstStaticBlock : AstBlock
{
    public AstStaticBlock(string? source, Position startLoc, Position endLoc, ref StructList<AstNode> body)
        : base(source, startLoc, endLoc, ref body)
    {
    }

    AstStaticBlock() : base(null, default, default)
    {
    }

    public override AstNode ShallowClone()
    {
        var res = new AstStaticBlock();
        res.Body.AddRange(Body.AsReadOnlySpan());
        res.Source = Source;
        res.Start = Start;
        res.End = End;
        return res;
    }

    public override void CodeGen(OutputContext output)
    {
        output.Print("static");
        output.Space();
        base.CodeGen(output);
    }
}
