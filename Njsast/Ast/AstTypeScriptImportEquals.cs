using Njsast.Reader;

namespace Njsast.Ast;

public class AstTypeScriptImportEquals : AstVar
{
    public AstTypeScriptImportEquals(string? source, Position startPos, Position endPos,
        ref StructRefList<AstVarDef> definitions) : base(source, startPos, endPos, ref definitions)
    {
    }

    public override AstNode ShallowClone()
    {
        var definitions = new StructRefList<AstVarDef>();
        definitions.AddRange(Definitions.AsReadOnlySpan());
        return new AstTypeScriptImportEquals(Source, Start, End, ref definitions);
    }
}

public class AstTypeScriptImportEqualsConst : AstConst
{
    public AstTypeScriptImportEqualsConst(string? source, Position startPos, Position endPos,
        ref StructRefList<AstVarDef> definitions) : base(source, startPos, endPos, ref definitions)
    {
    }

    public override AstNode ShallowClone()
    {
        var definitions = new StructRefList<AstVarDef>();
        definitions.AddRange(Definitions.AsReadOnlySpan());
        return new AstTypeScriptImportEqualsConst(Source, Start, End, ref definitions);
    }
}
