using Njsast.AstDump;
using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast;

public class AstComputedPropertyKey : AstNode
{
    public AstNode Expression;

    public AstComputedPropertyKey(string? source, Position startLoc, Position endLoc, AstNode expression) : base(
        source, startLoc, endLoc)
    {
        Expression = expression;
    }

    AstComputedPropertyKey(AstNode expression)
    {
        Expression = expression;
    }

    public override AstNode ShallowClone()
    {
        return new AstComputedPropertyKey(Source, Start, End, Expression);
    }

    public override void Visit(TreeWalker w)
    {
        base.Visit(w);
        w.Walk(Expression);
    }

    public override void Transform(TreeTransformer tt)
    {
        base.Transform(tt);
        Expression = tt.Transform(Expression);
    }

    public override void CodeGen(OutputContext output)
    {
        Expression.Print(output);
    }

    public override void DumpScalars(IAstDumpWriter writer)
    {
        base.DumpScalars(writer);
    }
}
