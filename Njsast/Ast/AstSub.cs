using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast;

/// Index-style property access, i.e. `a["foo"]` or `a?.[42]`
public class AstSub : AstPropAccess
{
    public AstSub(string? source, Position startLoc, Position endLoc, AstNode expression, AstNode property, bool optional = false) : base(
        source, startLoc, endLoc, expression, property, optional)
    {
    }

    public override AstNode ShallowClone()
    {
        return new AstSub(Source, Start, End, Expression, (AstNode)Property, Optional);
    }

    public override void CodeGen(OutputContext output)
    {
        Expression.Print(output);
        output.Print(Optional ? "?.[" : "[");
        ((AstNode) Property).Print(output);
        output.Print("]");
    }

    public override bool IsStructurallyEquivalentTo(AstNode? with)
    {
        if (with is AstSub withSub)
        {
            return Expression.IsStructurallyEquivalentTo(withSub.Expression) &&
                   ((AstNode) Property).IsStructurallyEquivalentTo((AstNode) withSub.Property);
        }

        return false;
    }

    public override bool IsConstantLike()
    {
        return Expression.IsConstantLike() && ((AstNode) Property).IsConstantLike();
    }
}
