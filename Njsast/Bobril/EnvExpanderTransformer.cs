using System;
using System.Collections.Generic;
using Njsast.Ast;

namespace Njsast.Bobril;

public class EnvExpanderTransformer : TreeTransformer
{
    readonly IReadOnlyDictionary<string, AstNode> _globalConstants;
    readonly Func<string, string?> _envGetter;
    readonly Func<string, string?> _fileGetter;

    public EnvExpanderTransformer(IReadOnlyDictionary<string, AstNode> globalConstants,
        Func<string, string?> envGetter, Func<string, string?>? fileGetter)
    {
        _globalConstants = globalConstants;
        _envGetter = envGetter;
        _fileGetter = fileGetter ?? (s=>null);
    }

    protected override AstNode? Before(AstNode node, bool inList)
    {
        if (node is AstPropAccess propAccess && propAccess.Expression.IsSymbolDef().IsGlobalSymbol() == "env" &&
            propAccess.PropertyAsString is {} propName)
        {
            var value = _envGetter(propName);
            if (value is {}) return new AstString(value);
            return AstUndefined.Instance;
        }
            
        if (node is AstPropAccess propAccess2 && propAccess2.Expression.IsSymbolDef().IsGlobalSymbol() == "file" &&
            propAccess2.PropertyAsString is {} propName2)
        {
            var value = _fileGetter(propName2);
            if (value is {}) return new AstString(value);
            return AstUndefined.Instance;
        }

        if (node.IsSymbolDef().IsGlobalSymbol() is {} name && _globalConstants.TryGetValue(name, out var globalValue))
        {
            return globalValue;
        }

        return null;
    }

    protected override AstNode? After(AstNode node, bool inList)
    {
        return node;
    }
}