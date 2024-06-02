using Njsast.Ast;
using Njsast.Reader;

namespace Njsast.Compress;

public class DetectPurposeTransformer : TreeTransformer
{
    protected override AstNode? Before(AstNode node, bool inList)
    {
        switch (node)
        {
            case AstLambda lambda:
            {
                if (lambda is AstArrow && lambda.Body is { Count: 1 } body2 &&
                    body2[0] is AstReturn { Value: not null } astReturn)
                {
                    lambda.Body[0] = astReturn.Value;
                }

                if (lambda.Body.Count == 0) lambda.Pure = true;
                lambda.Purpose ??= DetectPurpose(lambda);
                return null;
            }

            case AstCall:
            {
                node.Transform(this);
                if (node is AstCall
                    {
                        Expression: AstLambda { Purpose: EnumDefinitionPurpose purpose }, Args.Count: 1
                    } call)
                {
                    if (call.Args[0].IsSymbolDef() is { } symbolDef)
                    {
                        if (symbolDef.References.Count == 1)
                        {
                            return Remove;
                        }

                        symbolDef.Purpose ??= purpose;
                    }
                    else if (call.Args[0] is AstBinary
                             {
                                 Operator: Operator.LogicalOr, Left: AstSymbolRef left,
                                 Right: AstAssign { Operator: Operator.Assignment, Left: { } left2, Right: AstAssign { Operator: Operator.Assignment } }
                             }
                            && left.IsSymbolDef() is { } symbolDef2 && left2.IsSymbolDef() is { } symbolDef3)
                    {
                        symbolDef2.Purpose ??= purpose;
                        symbolDef3.Purpose ??= purpose;
                    }
                }

                return node;
            }
        }

        return null;
    }

    static IPurpose DetectPurpose(AstLambda lambda)
    {
        if (Helpers.DetectEnumTypeScriptFunction(lambda) is { } enumValues)
        {
            return new EnumDefinitionPurpose(enumValues);
        }

        return NoPurpose.Instance;
    }

    protected override AstNode? After(AstNode node, bool inList)
    {
        return null;
    }
}
