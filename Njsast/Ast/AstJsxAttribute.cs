using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast;

public abstract class AstJsxAttributeLike : AstNode
{
    protected AstJsxAttributeLike(string? source, Position startLoc, Position endLoc) : base(source, startLoc, endLoc)
    {
    }

    protected AstJsxAttributeLike()
    {
    }
}

public class AstJsxAttribute : AstJsxAttributeLike
{
    public AstJsxNameBase Name;
    public AstNode? Value;

    public AstJsxAttribute(string? source, Position startLoc, Position endLoc, AstJsxNameBase name, AstNode? value) :
        base(source, startLoc, endLoc)
    {
        Name = name;
        Value = value;
    }

    AstJsxAttribute(AstJsxNameBase name, AstNode? value)
    {
        Name = name;
        Value = value;
    }

    public override void Visit(TreeWalker w)
    {
        base.Visit(w);
        w.Walk(Name);
        w.Walk(Value);
    }

    public override void Transform(TreeTransformer tt)
    {
        base.Transform(tt);
        Name = (AstJsxNameBase)tt.Transform(Name);
        if (Value != null)
            Value = tt.Transform(Value);
    }

    public override AstNode ShallowClone()
    {
        return new AstJsxAttribute(Name, Value) { Source = Source, Start = Start, End = End };
    }

    public override void CodeGen(OutputContext output)
    {
        Name.Print(output);
        if (Value == null)
            return;
        output.Print("=");
        if (Value is AstString str)
        {
            str.Print(output);
        }
        else if (Value is AstJsxElement or AstJsxFragment or AstJsxExpression)
        {
            Value.Print(output);
        }
        else
        {
            output.Print("{");
            Value.Print(output);
            output.Print("}");
        }
    }
}

public class AstJsxSpreadAttribute : AstJsxAttributeLike
{
    public AstNode Expression;

    public AstJsxSpreadAttribute(string? source, Position startLoc, Position endLoc, AstNode expression) : base(source,
        startLoc, endLoc)
    {
        Expression = expression;
    }

    AstJsxSpreadAttribute(AstNode expression)
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
        return new AstJsxSpreadAttribute(Expression) { Source = Source, Start = Start, End = End };
    }

    public override void CodeGen(OutputContext output)
    {
        output.Print("{...");
        Expression.Print(output);
        output.Print("}");
    }
}
