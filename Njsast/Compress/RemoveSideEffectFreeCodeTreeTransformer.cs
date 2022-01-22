using Njsast.Ast;
using Njsast.Reader;
using Njsast.Runtime;

namespace Njsast.Compress;

public class GatherVarDeclarations : TreeWalker
{
    AstNode _result = TreeTransformer.Remove;

    public static AstNode Calc(AstNode? node)
    {
        if (node == null || node == TreeTransformer.Remove) return TreeTransformer.Remove;
        var w = new GatherVarDeclarations();
        w.Walk(node);
        return w._result;
    }

    protected override void Visit(AstNode node)
    {
        switch (node)
        {
            case AstVar astVar:
            {
                if (_result == TreeTransformer.Remove)
                {
                    _result = astVar;
                }

                foreach (var varDef in astVar.Definitions)
                {
                    varDef.Value = null;
                    ((AstVar)_result).Definitions.AddUnique(varDef);
                }

                StopDescending();
                break;
            }
            case AstLet _:
            case AstConst _:
            case AstSimpleStatement _:
            case AstDefinitions _:
            case AstLambda _:
            case AstClass _:
            {
                StopDescending();
                break;
            }
        }
    }
}

public class RemoveSideEffectFreeCodeTreeTransformer : TreeTransformer
{
    bool NeedValue = true;

    // Symbol is considered cloned when there is just single initialization by constant symbol
    // var a = 42; var b = a;
    // b could be replaced by a and that what this map contains _clonedSymbolMap[Symbol"b"] = Symbol"a";
    readonly RefDictionary<SymbolDef, SymbolDef> _clonedSymbolMap = new RefDictionary<SymbolDef, SymbolDef>();

    protected override AstNode? Before(AstNode node, bool inList)
    {
        if (NeedValue)
        {
            switch (node)
            {
                case AstEmptyStatement _:
                {
                    return inList ? Remove : node;
                }
                case AstWith _:
                    return node;
                case AstSymbolRef symbolRef:
                {
                    if (!symbolRef.Usage.HasFlag(SymbolUsage.Write) &&
                        _clonedSymbolMap.TryGetValue(symbolRef.Thedef!, out var replaceSymbol))
                    {
                        return new AstSymbolRef(node, replaceSymbol, symbolRef.Usage);
                    }

                    return node;
                }
                case AstLambda lambda:
                {
                    if (lambda.Body.Count == 0) lambda.Pure = true;
                    if (lambda.Purpose == null)
                        lambda.Purpose = DetectPurpose(lambda);

                    NeedValue = lambda.Body is { Count: 1 } body && body[0].IsExpression();
                    TransformList(ref lambda.Body);
                    NeedValue = true;
                    return node;
                }
                case AstBlock block:
                {
                    // Need value for Block is nonsense - optimize like it would not be needed
                    OptimizeBlock(block);

                    NeedValue = false;
                    TransformList(ref block.Body);
                    NeedValue = true;
                    return block;
                }
                case AstAssign assign:
                {
                    if (assign.Operator == Operator.Assignment)
                    {
                        var leftSymbol = assign.Left.IsSymbolDef();
                        if (leftSymbol?.NeverRead ?? false)
                        {
                            return Transform(assign.Right);
                        }

                        var rightSymbol = assign.Right.IsSymbolDef();
                        if (rightSymbol is { } && leftSymbol is
                            {
                                Global: false, Orig.Count: 1
                            } && leftSymbol.Orig[0] is AstSymbolDeclaration
                            {
                                Init: AstVarDef
                                {
                                    Value: null
                                }
                            } && Parent() is AstCall { Expression: AstLambda { Purpose: EnumDefinitionPurpose } } &&
                            leftSymbol.References.All(r =>
                                r == assign.Left || !r.Usage.HasFlag(SymbolUsage.Write | SymbolUsage.PropWrite)))
                        {
                            foreach (var leftSymbolReference in leftSymbol.References)
                            {
                                if (leftSymbolReference == assign.Left) continue;
                                leftSymbolReference.Name = rightSymbol.Name;
                                leftSymbolReference.Thedef = rightSymbol;
                                rightSymbol.References.Add(leftSymbolReference);
                            }

                            return assign.Right;
                        }
                    }

                    return null;
                }
                case AstBinary binary:
                {
                    if (binary.Operator == Operator.LogicalOr)
                    {
                        var b = binary.Left.ConstValue();
                        if (b != null)
                        {
                            if (TypeConverter.ToBoolean(b))
                            {
                                return Transform(binary.Left);
                            }

                            return Transform(binary.Right);
                        }

                        // x || (x = {}) when x is only written in this expression
                        if (binary.Left.IsSymbolDef() is { } symbolX
                            && binary.Right is AstAssign { Operator: Operator.Assignment } astAssign
                            && astAssign.Left.IsSymbolDef() == symbolX
                            && astAssign.Right is AstObject { Properties.Count: 0 }
                            && symbolX.References.All(r =>
                                r == astAssign.Left || !r.Usage.HasFlag(SymbolUsage.Write | SymbolUsage.PropWrite)))
                        {
                            if (symbolX.Orig is not { Count: 1 } || symbolX.Orig[0] is not AstSymbolVar
                                {
                                    Init: AstVarDef varDef
                                } symbolVar) return Transform(binary.Right);
                            varDef.Value = astAssign.Right;
                            symbolVar.Usage = SymbolUsage.Write;
                            return binary.Left;
                        }
                    }

                    if (binary.Operator == Operator.NullishCoalescing)
                    {
                        var b = binary.Left.ConstValue();
                        if (b != null)
                        {
                            return Transform(b is not AstNull and not AstUndefined ? binary.Left : binary.Right);
                        }
                    }

                    if (binary.Operator == Operator.LogicalAnd)
                    {
                        var b = binary.Left.ConstValue();
                        if (b != null)
                        {
                            return Transform(!TypeConverter.ToBoolean(b) ? binary.Left : binary.Right);
                        }
                    }

                    return null;
                }
                case AstCall call:
                {
                    if (call.Expression is AstLambda { Purpose: { } purpose and EnumDefinitionPurpose } && call.Args is
                        {
                            Count: 1
                        } && call.Args[0].IsSymbolDef() is { } symbolDef)
                    {
                        if (symbolDef.References.Count == 1)
                        {
                            return Remove;
                        }

                        symbolDef.Purpose ??= purpose;
                    }

                    return null;
                }
                case AstPropAccess propAccess:
                {
                    if (propAccess.Expression.IsSymbolDef() is
                            { Purpose: EnumDefinitionPurpose enumDefinitionPurpose } &&
                        propAccess.PropertyAsString is { } propName &&
                        enumDefinitionPurpose.Values.TryGetValue(propName, out var value))
                    {
                        return value;
                    }

                    return null;
                }
                case AstPrefixedTemplateString prefixedTemplateString:
                {
                    if (prefixedTemplateString.TemplateString.Segments.Count == 1 &&
                        prefixedTemplateString.TemplateString.Segments[0] is AstTemplateSegment s &&
                        prefixedTemplateString.Prefix is AstDot { PropertyAsString: "raw" } dot &&
                        dot.Expression.IsSymbolDef().IsGlobalSymbol() == "String")
                    {
                        return new AstString(s.Source, s.Start, s.End, s.Raw);
                    }

                    break;
                }
            }

            return null;
        }

        while (true)
        {
            switch (node)
            {
                case AstEmptyStatement _:
                {
                    return inList ? Remove : node;
                }
                case AstConstant _:
                    return Remove;
                case AstSymbolRef _:
                    return Remove;
                case AstThis _:
                    return Remove;
                case AstWith _:
                    return node;
                case AstPrefixedTemplateString prefixedTemplate:
                    if (IsWellKnownPureFunction(prefixedTemplate.Prefix, false))
                    {
                        node = prefixedTemplate.TemplateString;
                        continue;
                    }

                    return node;
                case AstTemplateString templateString:
                {
                    var res = new AstSequence(node.Source, node.Start, node.End);
                    foreach (var element in templateString.Segments)
                    {
                        if (element is AstTemplateSegment) continue;
                        res.AddIntelligently(Transform(element));
                    }

                    return res.Expressions.Count == 0 ? Remove : res;
                }
                case AstArray arr:
                {
                    var res = new AstSequence(node.Source, node.Start, node.End);
                    foreach (var element in arr.Elements)
                    {
                        res.AddIntelligently(Transform(element));
                    }

                    return res.Expressions.Count == 0 ? Remove : res;
                }
                case AstPropAccess propAccess:
                {
                    var globalSymbol = propAccess.Expression.IsSymbolDef().IsGlobalSymbol();
                    var propName = propAccess.PropertyAsString;
                    if (IsSideEffectFreePropertyAccess(globalSymbol, propName))
                    {
                        return Remove;
                    }

                    if (globalSymbol is "window" or "globalThis")
                    {
                        node = propAccess.Property as AstNode ?? Remove;
                        if (node == Remove)
                            return Remove;
                        continue;
                    }

                    goto default;
                }

                case AstObject obj:
                {
                    var res = new AstSequence(node.Source, node.Start, node.End);
                    foreach (var objProperty in obj.Properties)
                    {
                        if (objProperty is AstObjectKeyVal kv)
                        {
                            if (!(kv.Key is AstSymbolProperty))
                                res.AddIntelligently(Transform(kv.Key));
                            res.AddIntelligently(Transform(kv.Value));
                        }
                    }

                    return res.Expressions.Count switch
                    {
                        0 => Remove,
                        1 => res.Expressions[0],
                        _ => res
                    };
                }
                case AstUnaryPrefix unaryPrefix:
                {
                    if (unaryPrefix.Operator == Operator.LogicalNot)
                    {
                        if (unaryPrefix.Expression is AstCall { Expression: AstFunction })
                            goto default;
                        node = unaryPrefix.Expression;
                        continue;
                    }

                    if (unaryPrefix.Operator is Operator.TypeOf or Operator.BitwiseNot or Operator.Void
                        or Operator.Subtraction or Operator.Addition)
                    {
                        node = unaryPrefix.Expression;
                        continue;
                    }

                    goto default;
                }
                case AstClass defClass:
                {
                    if (defClass.Name == null || defClass.Name.Thedef!.OnlyDeclared)
                    {
                        return Remove;
                    }

                    goto default;
                }
                case AstLambda lambda:
                {
                    if (lambda.Name == null || lambda.Name.Thedef!.OnlyDeclared)
                    {
                        return Remove;
                    }

                    if (lambda.Name.Thedef.References.Count == CountReferences(lambda, lambda.Name.Thedef))
                    {
                        return Remove;
                    }

                    if (lambda.Body.Count == 0) lambda.Pure = true;
                    if (lambda.Purpose == null)
                        lambda.Purpose = DetectPurpose(lambda);

                    TransformList(ref lambda.Body);
                    return node;
                }
                case AstCall call:
                {
                    var symbol = call.Expression.IsSymbolDef();
                    if (symbol is { IsSingleInit: true, Init: AstLambda { Pure: true } } ||
                        IsWellKnownPureFunction(call.Expression, call is AstNew))
                    {
                        var res = new AstSequence(node.Source, node.Start, node.End);
                        foreach (var arg in call.Args)
                        {
                            res.AddIntelligently(Transform(arg));
                        }

                        return res.Expressions.Count switch
                        {
                            0 => Remove,
                            1 => res.Expressions[0],
                            _ => res
                        };
                    }

                    if (call.Expression is AstLambda { Purpose: { } purpose and EnumDefinitionPurpose } && call.Args is
                        {
                            Count: 1
                        } && call.Args[0].IsSymbolDef() is { } symbolDef)
                    {
                        if (symbolDef.References.Count == 1)
                        {
                            return Remove;
                        }

                        symbolDef.Purpose ??= purpose;
                    }

                    goto default;
                }
                case AstAssign assign:
                {
                    if (assign.Operator == Operator.Assignment)
                    {
                        if (assign.Left.IsSymbolDef()?.NeverRead ?? false)
                        {
                            node = assign.Right;
                            continue;
                        }
                    }

                    NeedValue = true;
                    assign.Right = Transform(assign.Right);
                    NeedValue = false;

                    return node;
                }
                case AstBinary binary:
                {
                    if (binary.Operator == Operator.LogicalOr)
                    {
                        var b = binary.Left.ConstValue();
                        if (b != null)
                        {
                            if (TypeConverter.ToBoolean(b))
                            {
                                node = binary.Left;
                                continue;
                            }

                            node = binary.Right;
                            continue;
                        }

                        var right = Transform(binary.Right);
                        if (right == Remove)
                        {
                            node = binary.Left;
                            continue;
                        }

                        binary.Right = right;
                        return node;
                    }

                    if (binary.Operator == Operator.NullishCoalescing)
                    {
                        var b = binary.Left.ConstValue();
                        if (b != null)
                        {
                            if (b is not AstNull and not AstUndefined)
                            {
                                node = binary.Left;
                                continue;
                            }

                            node = binary.Right;
                            continue;
                        }

                        var right = Transform(binary.Right);
                        if (right == Remove)
                        {
                            node = binary.Left;
                            continue;
                        }

                        binary.Right = right;
                        return node;
                    }

                    if (binary.Operator == Operator.LogicalAnd)
                    {
                        var b = binary.Left.ConstValue();
                        if (b != null)
                        {
                            if (!TypeConverter.ToBoolean(b))
                            {
                                node = binary.Left;
                                continue;
                            }

                            node = binary.Right;
                            continue;
                        }

                        var right = Transform(binary.Right);
                        if (right == Remove)
                        {
                            node = binary.Left;
                            continue;
                        }

                        binary.Right = right;
                        return node;
                    }

                    var res = new AstSequence(node.Source, node.Start, node.End);
                    res.AddIntelligently(Transform(binary.Left));
                    res.AddIntelligently(Transform(binary.Right));
                    return res.Expressions.Count switch
                    {
                        0 => Remove,
                        1 => res.Expressions[0],
                        _ => res
                    };
                }
                case AstVarDef varDef:
                {
                    var def = varDef.Name.IsSymbolDef();
                    if (def != null)
                    {
                        if (def.OnlyDeclared)
                        {
                            var value = varDef.Value;
                            if (value == null)
                                return Remove;
                            node = value;
                            continue;
                        }

                        if (varDef.Value is AstLambda && def.References.Count == CountReferences(varDef.Value, def))
                        {
                            return Remove;
                        }

                        if (varDef.Value.IsSymbolDef() is { } rightSymbolDef && def.IsSingleInit &&
                            rightSymbolDef.IsSingleInit)
                        {
                            _clonedSymbolMap.GetOrAddValueRef(def) = rightSymbolDef;
                        }
                    }

                    goto default;
                }
                case AstSimpleStatement simple:
                {
                    node.Transform(this);
                    return simple.Body == Remove
                        ? inList ? Remove : new AstEmptyStatement(node.Source, node.Start, node.End)
                        : simple;
                }
                case AstIf astIf:
                {
                    var b = astIf.Condition.ConstValue();
                    if (b != null)
                    {
                        return TypeConverter.ToBoolean(b)
                            ? MakeBlockFrom(astIf, GatherVarDeclarations.Calc(astIf.Alternative),
                                Transform(astIf.Body))
                            : MakeBlockFrom(astIf, GatherVarDeclarations.Calc(astIf.Body),
                                astIf.Alternative != null ? Transform(astIf.Alternative) : Remove);
                    }

                    NeedValue = true;
                    astIf.Condition = Transform(astIf.Condition);
                    NeedValue = false;
                    astIf.Body = (AstStatement)Transform(astIf.Body);

                    if (astIf.Alternative != null)
                    {
                        var alternative = Transform(astIf.Alternative);
                        astIf.Alternative = alternative == Remove ? null : (AstStatement)alternative;
                    }

                    return node;
                }
                case AstBlock block:
                {
                    OptimizeBlock(block);

                    if (block is AstCase astCase)
                    {
                        NeedValue = true;
                        astCase.Expression = Transform(astCase.Expression);
                        NeedValue = false;
                    }

                    TransformList(ref block.Body);
                    return node;
                }
                case AstForIn forIn:
                {
                    NeedValue = true;
                    forIn.Object = Transform(forIn.Object);
                    NeedValue = false;
                    forIn.Body = (AstStatement)Transform(forIn.Body);
                    return node;
                }
                case AstFor astFor:
                {
                    if (astFor.Init != null)
                    {
                        var init = Transform(astFor.Init);
                        if (init == Remove) init = null;
                        astFor.Init = init;
                    }

                    if (astFor.Condition != null)
                    {
                        NeedValue = true;
                        var cond = Transform(astFor.Condition);
                        if (cond == Remove) cond = null;
                        astFor.Condition = cond;
                        NeedValue = false;
                    }

                    if (astFor.Step != null)
                    {
                        var step = Transform(astFor.Step);
                        if (step == Remove) step = null;
                        astFor.Step = step;
                    }

                    astFor.Body = (AstStatement)Transform(astFor.Body);
                    return node;
                }
                case AstDefinitions def:
                    if (inList)
                    {
                        var defsResult = new StructList<AstNode>();
                        defsResult.Reserve(def.Definitions.Count);
                        var wasChange = 0;
                        foreach (var defi in def.Definitions)
                        {
                            var defo = Transform(defi);
                            if (defo == Remove)
                            {
                                wasChange |= 1;
                                continue;
                            }

                            if (defo != defi) wasChange |= 2;
                            defsResult.Add(defo);
                        }

                        if (wasChange != 0)
                        {
                            Modified = true;
                            if (defsResult.Count == 0)
                                return Remove;
                            if (wasChange == 1)
                            {
                                def.Definitions.Clear();
                                foreach (var defi in defsResult)
                                {
                                    def.Definitions.Add((AstVarDef)defi);
                                }

                                return node;
                            }

                            for (var i = 0; i < defsResult.Count; i++)
                            {
                                var defi = defsResult[i];
                                if (defi is AstVarDef)
                                {
                                    var me = (AstDefinitions)node.ShallowClone();
                                    me.Definitions.ClearAndTruncate();
                                    me.Definitions.Add((AstVarDef)defi);
                                    defsResult[i] = me;
                                }
                                else
                                {
                                    defsResult[i] = new AstSimpleStatement(defi);
                                }
                            }

                            return SpreadStructList(ref defsResult);
                        }

                        return node;
                    }
                    else if (def.Definitions.Count == 1)
                    {
                        var defi = def.Definitions[0];
                        var defo = Transform(defi);
                        if (defo == Remove) return Remove;
                        if (defi != defo)
                        {
                            Modified = true;
                            return new AstSimpleStatement(defo);
                        }
                    }

                    return node;
                default:
                    NeedValue = true;
                    node.Transform(this);
                    NeedValue = false;
                    return node;
            }
        }
    }

    void OptimizeBlock(AstBlock block)
    {
        for (var i = 0; i < block.Body.Count; i++)
        {
            var si = block.Body[i];
            if (si is AstVar astVar && i < block.Body.Count - 1)
            {
                var si2 = block.Body[i + 1];
                if (si2 is AstVar astVar2)
                {
                    astVar.Definitions.AddRange(astVar2.Definitions);
                    block.Body.RemoveAt(i + 1);
                    Modified = true;
                    i--;
                }
            }
            else if (si is AstLet astLet && i < block.Body.Count - 1)
            {
                var si2 = block.Body[i + 1];
                if (si2 is AstLet astLet2)
                {
                    astLet.Definitions.AddRange(astLet2.Definitions);
                    block.Body.RemoveAt(i + 1);
                    Modified = true;
                    i--;
                }
            }
            else if (si is AstConst astConst && i < block.Body.Count - 1)
            {
                var si2 = block.Body[i + 1];
                if (si2 is AstConst astConst2)
                {
                    astConst.Definitions.AddRange(astConst2.Definitions);
                    block.Body.RemoveAt(i + 1);
                    Modified = true;
                    i--;
                }
            }
            else if (si is AstBlockStatement statement)
            {
                if (statement.BlockScope!.IsSafelyInlinenable())
                {
                    block.Body.ReplaceItemAt(i, statement.Body.AsReadOnlySpan());
                    Modified = true;
                    i--;
                }
            }
        }
    }

    static IPurpose DetectPurpose(AstLambda lambda)
    {
        if (Helpers.DetectEnumTypeScriptFunction(lambda) is { } enumValues)
        {
            return new EnumDefinitionPurpose(enumValues);
        }

        return NoPurpose.Instance;
    }

    class CountReferencesWalker : TreeWalker
    {
        readonly SymbolDef _def;
        internal uint Refs;

        public CountReferencesWalker(SymbolDef def)
        {
            _def = def;
        }

        protected override void Visit(AstNode node)
        {
            if (node is AstSymbolRef symbolRef && symbolRef.Thedef == _def)
                Refs++;
        }
    }

    static uint CountReferences(AstNode node, SymbolDef def)
    {
        var walker = new CountReferencesWalker(def);
        walker.Walk(node);
        return walker.Refs;
    }

    static AstNode MakeBlockFrom(AstNode from, params AstNode[] statements)
    {
        var s = new StructList<AstNode>();
        foreach (var node in statements)
        {
            if (node != Remove) s.Add(node);
        }

        if (s.Count == 0)
            return Remove;
        if (s.Count == 1)
            return s[0];
        var res = new AstBlockStatement(@from.Source, @from.Start, @from.End, ref s);
        return res;
    }

    static bool IsWellKnownPureFunction(AstNode callExpression, bool isNew)
    {
        if (callExpression is AstFunction classFactory && (classFactory.Pure ?? false))
        {
            return true;
        }

        if (isNew && callExpression.IsSymbolDef().IsGlobalSymbol() is "Map" or "HashSet" or "Set" or "HashSet")
            return true;

        if (callExpression is AstPropAccess propAccess)
        {
            var globalSymbol = propAccess.Expression.IsSymbolDef().IsGlobalSymbol();
            var propName = propAccess.PropertyAsString;
            return IsWellKnownPureFunction(globalSymbol, propName);
        }

        return false;
    }

    static bool IsWellKnownPureFunction(string? globalSymbol, string? propName)
    {
        return globalSymbol switch
        {
            "String" => propName switch
            {
                "raw" => true,
                "fromCharCode" => true,
                "fromCodePoint" => true,
                _ => false
            },
            "Object" => propName switch
            {
                "constructor" => true,
                "create" => true,
                "entries" => true,
                "fromEntries" => true,
                "getOwnPropertyDescriptor" => true,
                "getOwnPropertyDescriptors" => true,
                "getOwnPropertyNames" => true,
                "getOwnPropertySymbols" => true,
                "getPrototypeOf" => true,
                "hasOwnProperty" => true,
                "is" => true,
                "isExtensible" => true,
                "isFrozen" => true,
                "isPrototypeOf" => true,
                "isSealed" => true,
                "keys" => true,
                "propertyIsEnumerable" => true,
                "prototype" => true,
                "toLocaleString" => true,
                "toString" => true,
                "valueOf" => true,
                "values" => true,
                _ => false
            },
            "Math" => propName switch
            {
                "abs" => true,
                "acos" => true,
                "acosh" => true,
                "asin" => true,
                "asinh" => true,
                "atan" => true,
                "atan2" => true,
                "atanh" => true,
                "cbrt" => true,
                "ceil" => true,
                "clz32" => true,
                "cos" => true,
                "cosh" => true,
                "exp" => true,
                "expm1" => true,
                "floor" => true,
                "fround" => true,
                "hypot" => true,
                "imul" => true,
                "log" => true,
                "log1p" => true,
                "log2" => true,
                "log10" => true,
                "max" => true,
                "min" => true,
                "pow" => true,
                "round" => true,
                "sign" => true,
                "sin" => true,
                "sinh" => true,
                "sqrt" => true,
                "tan" => true,
                "tanh" => true,
                "trunc" => true,
                _ => false
            },
            _ => false
        };
    }

    static bool IsSideEffectFreePropertyAccess(string? globalSymbol, string? propName)
    {
        return globalSymbol switch
        {
            "Object" => propName switch
            {
                "assign" => true,
                "constructor" => true,
                "create" => true,
                "defineProperties" => true,
                "defineProperty" => true,
                "entries" => true,
                "freeze" => true,
                "fromEntries" => true,
                "getOwnPropertyDescriptor" => true,
                "getOwnPropertyDescriptors" => true,
                "getOwnPropertyNames" => true,
                "getOwnPropertySymbols" => true,
                "getPrototypeOf" => true,
                "hasOwnProperty" => true,
                "is" => true,
                "isExtensible" => true,
                "isFrozen" => true,
                "isPrototypeOf" => true,
                "isSealed" => true,
                "keys" => true,
                "preventExtensions" => true,
                "propertyIsEnumerable" => true,
                "prototype" => true,
                "seal" => true,
                "setPrototypeOf" => true,
                "toLocaleString" => true,
                "toString" => true,
                "valueOf" => true,
                "values" => true,
                _ => false
            },
            "Math" => propName switch
            {
                "E" => true,
                "LN10" => true,
                "LN2" => true,
                "LOG10E" => true,
                "LOG2E" => true,
                "PI" => true,
                "SQRT1_2" => true,
                "SQRT2" => true,
                "abs" => true,
                "acos" => true,
                "acosh" => true,
                "asin" => true,
                "asinh" => true,
                "atan" => true,
                "atan2" => true,
                "atanh" => true,
                "cbrt" => true,
                "ceil" => true,
                "clz32" => true,
                "cos" => true,
                "cosh" => true,
                "exp" => true,
                "expm1" => true,
                "floor" => true,
                "fround" => true,
                "hypot" => true,
                "imul" => true,
                "log" => true,
                "log1p" => true,
                "log2" => true,
                "log10" => true,
                "max" => true,
                "min" => true,
                "pow" => true,
                "random" => true,
                "round" => true,
                "sign" => true,
                "sin" => true,
                "sinh" => true,
                "sqrt" => true,
                "tan" => true,
                "tanh" => true,
                "trunc" => true,
                _ => false
            },
            _ => false
        };
    }

    protected override AstNode? After(AstNode node, bool inList)
    {
        return null;
    }
}
