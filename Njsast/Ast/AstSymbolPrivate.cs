using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast;

/// A private name, e.g. #foo
public class AstSymbolPrivate : AstSymbol
{
    public AstSymbolPrivate(string? source, Position startLoc, Position endLoc, string name) 
        : base(source, startLoc, endLoc, name)
    {
    }

    AstSymbolPrivate(string name) : base(name)
    {
    }

    public override AstNode ShallowClone()
    {
        return new AstSymbolPrivate(Name) {Source = Source, Start = Start, End = End};
    }

    public override void CodeGen(OutputContext output)
    {
        output.Print("#");
        output.PrintName(Thedef?.MangledName ?? Thedef?.Name ?? Name);
    }
}
