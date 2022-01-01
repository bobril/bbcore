using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast;

/// The `import.meta` value
public class AstImportMeta : AstAtom
{
    public AstImportMeta(string? source, Position startLoc, Position endLoc) : base(source, startLoc, endLoc)
    {
    }

    public override AstNode ShallowClone()
    {
        return new AstImportMeta(Source, Start, End);
    }

    public override void CodeGen(OutputContext output)
    {
        output.Print("import.meta");
    }
}
