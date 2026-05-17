using Njsast.Reader;

namespace Njsast.Ast;

public class AstTypeScriptImportEquals : AstVar
{
    public AstTypeScriptImportEquals(string? source, Position startPos, Position endPos,
        ref StructList<AstVarDef> definitions) : base(source, startPos, endPos, ref definitions)
    {
    }

    public override AstNode ShallowClone()
    {
        var definitions = new StructList<AstVarDef>();
        definitions.AddRange(Definitions.AsReadOnlySpan());
        return new AstTypeScriptImportEquals(Source, Start, End, ref definitions);
    }
}
