using System.Globalization;
using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast;

public abstract class AstObjectItem : AstNode
{
    protected AstObjectItem(string? source, Position startPos, Position endPos) : base(source, startPos, endPos)
    {
    }

    protected AstObjectItem()
    {
    }
}

/// Base class for literal object properties
public abstract class AstObjectProperty : AstObjectItem
{
    /// [AstNode] property name.
    public AstNode Key;

    /// [AstNode] property value. For getters and setters this is an AstAccessor.
    public AstNode Value;

    protected AstObjectProperty(string? source, Position startLoc, Position endLoc, AstNode key, AstNode value) : base(
        source, startLoc, endLoc)
    {
        Key = key;
        Value = value;
    }

    protected AstObjectProperty(AstNode key, AstNode value)
    {
        Key = key;
        Value = value;
    }

    public override void Visit(TreeWalker w)
    {
        base.Visit(w);
        w.Walk(Key);
        w.Walk(Value);
    }

    public override void Transform(TreeTransformer tt)
    {
        base.Transform(tt);
        Key = tt.Transform(Key);
        Value = tt.Transform(Value);
    }

    protected void PrintGetterSetter(OutputContext output, string? type, bool @static)
    {
        if (@static)
        {
            output.Print("static");
            output.Space();
        }

        if (type != null)
        {
            output.Print(type);
            output.Space();
        }

        if (Key is AstString stringKey)
        {
            stringKey.Print(output);
        }
        else if (Key is AstSymbolRef)
        {
            output.Print("[");
            Key.Print(output);
            output.Print("]");
        }
        else if (Key switch
                 {
                     AstNumber num => num.Value.ToString("R", CultureInfo.InvariantCulture),
                     AstSymbolPrivate privateKey => "#" + privateKey.Name,
                     AstSymbol key => key.Name,
                     _ => null
                 } is { } keyString)
        {
            if (Key is AstSymbolPrivate)
                Key.Print(output);
            else
                output.PrintPropertyName(keyString);
        }
        else
        {
            output.Print("[");
            Key.Print(output);
            output.Print("]");
        }

        ((AstLambda) Value).DoPrint(output, true);
    }
}
