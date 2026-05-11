using Njsast.AstDump;
using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast;

public abstract class AstJsxNameBase : AstNode
{
    protected AstJsxNameBase(string? source, Position startLoc, Position endLoc) : base(source, startLoc, endLoc)
    {
    }

    protected AstJsxNameBase()
    {
    }

    public abstract string AsString();
}

public class AstJsxName : AstJsxNameBase
{
    public string Name;

    public AstJsxName(string? source, Position startLoc, Position endLoc, string name) : base(source, startLoc, endLoc)
    {
        Name = name;
    }

    AstJsxName(string name)
    {
        Name = name;
    }

    public override void DumpScalars(IAstDumpWriter writer)
    {
        base.DumpScalars(writer);
        writer.PrintProp("Name", Name);
    }

    public override AstNode ShallowClone()
    {
        return new AstJsxName(Name) { Source = Source, Start = Start, End = End };
    }

    public override void CodeGen(OutputContext output)
    {
        output.Print(Name);
    }

    public override string AsString()
    {
        return Name;
    }
}

public class AstJsxMemberName : AstJsxNameBase
{
    public AstJsxNameBase Expression;
    public AstJsxName Property;

    public AstJsxMemberName(string? source, Position startLoc, Position endLoc, AstJsxNameBase expression,
        AstJsxName property) : base(source, startLoc, endLoc)
    {
        Expression = expression;
        Property = property;
    }

    AstJsxMemberName(AstJsxNameBase expression, AstJsxName property)
    {
        Expression = expression;
        Property = property;
    }

    public override void Visit(TreeWalker w)
    {
        base.Visit(w);
        w.Walk(Expression);
        w.Walk(Property);
    }

    public override void Transform(TreeTransformer tt)
    {
        base.Transform(tt);
        Expression = (AstJsxNameBase)tt.Transform(Expression);
        Property = (AstJsxName)tt.Transform(Property);
    }

    public override AstNode ShallowClone()
    {
        return new AstJsxMemberName(Expression, Property) { Source = Source, Start = Start, End = End };
    }

    public override void CodeGen(OutputContext output)
    {
        Expression.Print(output);
        output.Print(".");
        Property.Print(output);
    }

    public override string AsString()
    {
        return Expression.AsString() + "." + Property.AsString();
    }
}

public class AstJsxNamespacedName : AstJsxNameBase
{
    public AstJsxName Namespace;
    public AstJsxName Name;

    public AstJsxNamespacedName(string? source, Position startLoc, Position endLoc, AstJsxName @namespace,
        AstJsxName name) : base(source, startLoc, endLoc)
    {
        Namespace = @namespace;
        Name = name;
    }

    AstJsxNamespacedName(AstJsxName @namespace, AstJsxName name)
    {
        Namespace = @namespace;
        Name = name;
    }

    public override void Visit(TreeWalker w)
    {
        base.Visit(w);
        w.Walk(Namespace);
        w.Walk(Name);
    }

    public override void Transform(TreeTransformer tt)
    {
        base.Transform(tt);
        Namespace = (AstJsxName)tt.Transform(Namespace);
        Name = (AstJsxName)tt.Transform(Name);
    }

    public override AstNode ShallowClone()
    {
        return new AstJsxNamespacedName(Namespace, Name) { Source = Source, Start = Start, End = End };
    }

    public override void CodeGen(OutputContext output)
    {
        Namespace.Print(output);
        output.Print(":");
        Name.Print(output);
    }

    public override string AsString()
    {
        return Namespace.AsString() + ":" + Name.AsString();
    }
}
