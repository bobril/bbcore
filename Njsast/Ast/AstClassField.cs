using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast;

/// A class field, e.g. `static x = 1;` or `y = 2;`
public class AstClassField : AstObjectProperty
{
    public bool Static;
    public bool Computed;

    public AstClassField(string? source, Position startLoc, Position endLoc, AstNode key, AstNode? value,
        bool @static, bool computed = false) : base(source, startLoc, endLoc, key,
        value ?? new AstUndefined(source, startLoc, endLoc))
    {
        Static = @static;
        Computed = computed;
    }

    AstClassField(AstNode key, AstNode value, bool @static, bool computed) : base(key, value)
    {
        Static = @static;
        Computed = computed;
    }

    public override AstNode ShallowClone()
    {
        return new AstClassField(Key, Value, Static, Computed) {Source = Source, Start = Start, End = End};
    }

    public override void CodeGen(OutputContext output)
    {
        if (Static)
        {
            output.Print("static");
            output.Space();
        }

        if (Computed)
        {
            output.Print("[");
            Key.Print(output);
            output.Print("]");
        }
        else
        {
            Key.Print(output);
        }

        if (Value is not AstUndefined)
        {
            output.Space();
            output.Print("=");
            output.Space();
            Value.Print(output);
        }

        output.Print(";");
    }
}
