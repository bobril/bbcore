using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast;

public class AstJsxElement : AstNode
{
    public AstJsxNameBase Name;
    public StructList<AstJsxAttributeLike> Attributes;
    public StructList<AstNode> Children;
    public bool SelfClosing;

    public AstJsxElement(string? source, Position startLoc, Position endLoc, AstJsxNameBase name,
        ref StructList<AstJsxAttributeLike> attributes, ref StructList<AstNode> children, bool selfClosing) : base(
        source, startLoc, endLoc)
    {
        Name = name;
        Attributes.TransferFrom(ref attributes);
        Children.TransferFrom(ref children);
        SelfClosing = selfClosing;
    }

    AstJsxElement(AstJsxNameBase name, bool selfClosing)
    {
        Name = name;
        SelfClosing = selfClosing;
    }

    public override void Visit(TreeWalker w)
    {
        base.Visit(w);
        w.Walk(Name);
        w.WalkList(Attributes);
        w.WalkList(Children);
    }

    public override void Transform(TreeTransformer tt)
    {
        base.Transform(tt);
        Name = (AstJsxNameBase)tt.Transform(Name);
        tt.TransformList(ref Attributes);
        tt.TransformList(ref Children);
    }

    public override AstNode ShallowClone()
    {
        var res = new AstJsxElement(Name, SelfClosing) { Source = Source, Start = Start, End = End };
        res.Attributes.AddRange(Attributes.AsReadOnlySpan());
        res.Children.AddRange(Children.AsReadOnlySpan());
        return res;
    }

    public override void CodeGen(OutputContext output)
    {
        output.Print("<");
        Name.Print(output);
        PrintAttributes(output, Attributes);
        if (SelfClosing)
        {
            output.Print(" ");
            output.Print("/>");
            return;
        }

        output.Print(">");
        foreach (var child in Children)
        {
            child.Print(output);
        }
        output.Print("</");
        Name.Print(output);
        output.Print(">");
    }

    internal static void PrintAttributes(OutputContext output, in StructList<AstJsxAttributeLike> attributes)
    {
        foreach (var attr in attributes)
        {
            output.Print(" ");
            attr.Print(output);
        }
    }
}

public class AstJsxFragment : AstNode
{
    public StructList<AstNode> Children;

    public AstJsxFragment(string? source, Position startLoc, Position endLoc, ref StructList<AstNode> children) : base(
        source, startLoc, endLoc)
    {
        Children.TransferFrom(ref children);
    }

    AstJsxFragment()
    {
    }

    public override void Visit(TreeWalker w)
    {
        base.Visit(w);
        w.WalkList(Children);
    }

    public override void Transform(TreeTransformer tt)
    {
        base.Transform(tt);
        tt.TransformList(ref Children);
    }

    public override AstNode ShallowClone()
    {
        var res = new AstJsxFragment { Source = Source, Start = Start, End = End };
        res.Children.AddRange(Children.AsReadOnlySpan());
        return res;
    }

    public override void CodeGen(OutputContext output)
    {
        output.Print("<>");
        foreach (var child in Children)
        {
            child.Print(output);
        }
        output.Print("</>");
    }
}
