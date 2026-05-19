using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast;

/// A class expression.
public class AstClassExpression : AstClass
{
    public AstClassExpression(string? source, Position startPos, Position endPos, AstSymbolDeclaration? name,
        AstNode? extends, ref StructList<AstNode> properties) : base(source, startPos, endPos, name,
        extends, ref properties)
    {
    }

    public AstClassExpression(string? source, Position startPos, Position endPos, AstSymbolDeclaration? name,
        AstNode? extends, ref StructRefList<AstNode> properties) : base(source, startPos, endPos, name,
        extends, ref properties)
    {
    }

    public override bool NeedParens(OutputContext output)
    {
        return output.FirstInStatement();
    }

    public override AstNode ShallowClone()
    {
        var prop = new StructList<AstNode>();
        prop.AddRange(Properties.AsReadOnlySpan());
        return new AstClassExpression(Source, Start, End, Name, Extends, ref prop);
    }
}
