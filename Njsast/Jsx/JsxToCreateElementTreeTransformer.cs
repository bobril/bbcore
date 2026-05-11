using System;
using Njsast.Ast;
using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Jsx;

public class JsxToCreateElementTreeTransformer : TreeTransformer
{
    readonly JsxToCreateElementOptions _options;

    public JsxToCreateElementTreeTransformer(JsxToCreateElementOptions? options = null)
    {
        _options = options ?? new JsxToCreateElementOptions();
    }

    protected override AstNode? Before(AstNode node, bool inList)
    {
        return null;
    }

    protected override AstNode? After(AstNode node, bool inList)
    {
        return node switch
        {
            AstJsxElement element => LowerElement(element),
            AstJsxFragment fragment => LowerFragment(fragment),
            AstJsxText text => new AstString(text.Source, text.Start, text.End, text.Raw),
            AstJsxExpression { Expression: null } => Remove,
            AstJsxExpression expression => expression.Expression,
            AstJsxSpreadChild spreadChild => new AstExpansion(spreadChild.Source, spreadChild.Start, spreadChild.End,
                spreadChild.Expression),
            _ => null
        };
    }

    AstNode LowerElement(AstJsxElement element)
    {
        var args = new StructList<AstNode>();
        args.Add(LowerElementName(element.Name));
        args.Add(LowerProps(element));
        AddChildren(ref args, element.Children);
        return new AstCall(element.Source, element.Start, element.End, BuildDottedExpression(_options.Factory), ref args);
    }

    AstNode LowerFragment(AstJsxFragment fragment)
    {
        var args = new StructList<AstNode>();
        args.Add(BuildDottedExpression(_options.Fragment));
        args.Add(MakeNull(fragment));
        AddChildren(ref args, fragment.Children);
        return new AstCall(fragment.Source, fragment.Start, fragment.End, BuildDottedExpression(_options.Factory), ref args);
    }

    static void AddChildren(ref StructList<AstNode> args, in StructList<AstNode> children)
    {
        for (var i = 0u; i < children.Count; i++)
        {
            args.Add(children[i]);
        }
    }

    AstNode LowerProps(AstJsxElement element)
    {
        if (element.Attributes.Count == 0)
            return MakeNull(element);

        var properties = new StructList<AstObjectItem>();
        for (var i = 0u; i < element.Attributes.Count; i++)
        {
            switch (element.Attributes[i])
            {
                case AstJsxAttribute attr:
                    properties.Add(LowerAttribute(attr));
                    break;
                case AstJsxSpreadAttribute spread:
                    properties.Add(new AstExpansion(spread.Source, spread.Start, spread.End, spread.Expression));
                    break;
            }
        }

        return new AstObject(element.Source, element.Start, element.End, ref properties);
    }

    static AstObjectKeyVal LowerAttribute(AstJsxAttribute attr)
    {
        var value = attr.Value switch
        {
            null => new AstTrue(attr.Source, attr.Start, attr.End),
            AstJsxExpression { Expression: null } => new AstTrue(attr.Source, attr.Start, attr.End),
            AstJsxExpression expression => expression.Expression ?? new AstTrue(attr.Source, attr.Start, attr.End),
            _ => attr.Value
        };

        return new AstObjectKeyVal(attr.Source, attr.Start, attr.End,
            new AstString(attr.Name.Source, attr.Name.Start, attr.Name.End, attr.Name.AsString()), value);
    }

    static AstNode LowerElementName(AstJsxNameBase name)
    {
        return name switch
        {
            AstJsxName jsxName when IsIntrinsicName(jsxName.Name) => new AstString(jsxName.Source, jsxName.Start,
                jsxName.End, jsxName.Name),
            AstJsxName jsxName => new AstSymbolRef(jsxName.Source, jsxName.Start, jsxName.End, jsxName.Name),
            AstJsxMemberName member => LowerMemberName(member),
            AstJsxNamespacedName namespaced => new AstString(namespaced.Source, namespaced.Start, namespaced.End,
                namespaced.AsString()),
            _ => throw new NotSupportedException($"Unsupported JSX name node {name.GetType().Name}.")
        };
    }

    static AstNode LowerMemberName(AstJsxMemberName name)
    {
        return new AstDot(LowerMemberExpression(name.Expression), name.Property.Name);
    }

    static AstNode LowerMemberExpression(AstJsxNameBase name)
    {
        return name switch
        {
            AstJsxName jsxName => new AstSymbolRef(jsxName.Source, jsxName.Start, jsxName.End, jsxName.Name),
            AstJsxMemberName member => LowerMemberName(member),
            _ => throw new NotSupportedException($"Unsupported JSX member expression node {name.GetType().Name}.")
        };
    }

    static bool IsIntrinsicName(string name)
    {
        return name.Length > 0 && char.IsLower(name[0]);
    }

    static AstNode BuildDottedExpression(string expression)
    {
        var parts = expression.Split('.');
        if (parts.Length == 0)
            throw new ArgumentException("Expression must not be empty.", nameof(expression));

        AstNode result = MakeSymbol(parts[0], expression);
        for (var i = 1; i < parts.Length; i++)
        {
            if (!OutputContext.IsIdentifier(parts[i]))
                throw new ArgumentException($"'{expression}' is not a valid dotted JavaScript expression.",
                    nameof(expression));
            result = new AstDot(result, parts[i]);
        }

        return result;
    }

    static AstSymbolRef MakeSymbol(string name, string expression)
    {
        if (!OutputContext.IsIdentifier(name))
            throw new ArgumentException($"'{expression}' is not a valid dotted JavaScript expression.",
                nameof(expression));
        return new AstSymbolRef(name);
    }

    static AstNull MakeNull(AstNode from)
    {
        return new AstNull(from.Source, from.Start, from.End);
    }
}
