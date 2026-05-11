using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast;

public class AstJsxExpression : AstNode
{
    public AstNode? Expression;

    public AstJsxExpression(string? source, Position startLoc, Position endLoc, AstNode? expression) : base(source,
        startLoc, endLoc)
    {
        Expression = expression;
    }

    AstJsxExpression(AstNode? expression)
    {
        Expression = expression;
    }

    public override void Visit(TreeWalker w)
    {
        base.Visit(w);
        w.Walk(Expression);
    }

    public override void Transform(TreeTransformer tt)
    {
        base.Transform(tt);
        if (Expression != null)
            Expression = tt.Transform(Expression);
    }

    public override AstNode ShallowClone()
    {
        return new AstJsxExpression(Expression) { Source = Source, Start = Start, End = End };
    }

    public override void CodeGen(OutputContext output)
    {
        output.Print("{");
        Expression?.Print(output);
        output.Print("}");
    }
}

public class AstJsxSpreadChild : AstNode
{
    public AstNode Expression;

    public AstJsxSpreadChild(string? source, Position startLoc, Position endLoc, AstNode expression) : base(source,
        startLoc, endLoc)
    {
        Expression = expression;
    }

    AstJsxSpreadChild(AstNode expression)
    {
        Expression = expression;
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

    public override AstNode ShallowClone()
    {
        return new AstJsxSpreadChild(Expression) { Source = Source, Start = Start, End = End };
    }

    public override void CodeGen(OutputContext output)
    {
        output.Print("{...");
        Expression.Print(output);
        output.Print("}");
    }
}
