using System.Collections.Generic;

namespace Njsast.Ast;

public interface IPurpose
{
}

public class NoPurpose : IPurpose
{
    public static readonly NoPurpose Instance = new();
}

public class EnumDefinitionPurpose : IPurpose
{
    public readonly Dictionary<string, AstNode> Values;

    public EnumDefinitionPurpose(Dictionary<string, AstNode> values)
    {
        Values = values;
    }
}