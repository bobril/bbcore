using Njsast.Ast;
using Njsast.Reader;
using Njsast.Runtime;

namespace Njsast.Compress
{
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
                        ((AstVar) _result).Definitions.AddUnique(varDef);
                    }

                    StopDescending();
                    break;
                }
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
                    case AstDefun defun:
                    {
                        if (defun.Name!.IsSymbolDef()!.OnlyDeclared)
                            return Remove;
                        if (defun.Body.Count == 0) defun.Pure = true;
                        return null;
                    }
                    case AstBlock block:
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

                        return null;
                    }
                    case AstAssign assign:
                    {
                        if (assign.Operator == Operator.Assignment)
                        {
                            if (assign.Left.IsSymbolDef()?.NeverRead ?? false)
                            {
                                return Transform(assign.Right);
                            }
                        }

                        return null;
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
                        }

                        if (binary.Operator == Operator.LogicalAnd)
                        {
                            var b = binary.Left.ConstValue();
                            if (b != null)
                            {
                                if (!TypeConverter.ToBoolean(b))
                                {
                                    return Transform(binary.Left);
                                }

                                return Transform(binary.Right);
                            }
                        }

                        return null;
                    }
                    case AstSimpleStatement simple:
                    {
                        NeedValue = false;
                        node.Transform(this);
                        NeedValue = true;
                        return simple.Body == Remove
                            ? inList ? Remove : new AstEmptyStatement(node.Source, node.Start, node.End)
                            : simple;
                    }
                    case AstDefinitions def:
                        NeedValue = false;
                        try
                        {
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
                                            def.Definitions.Add((AstVarDef) defi);
                                        }

                                        return node;
                                    }

                                    for (var i = 0; i < defsResult.Count; i++)
                                    {
                                        var defi = defsResult[i];
                                        if (defi is AstVarDef)
                                        {
                                            var me = (AstDefinitions) node.ShallowClone();
                                            me.Definitions.ClearAndTruncate();
                                            me.Definitions.Add((AstVarDef) defi);
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
                        }
                        finally
                        {
                            NeedValue = true;
                        }
                }

                return null;
            }

            while (true)
            {
                switch (node)
                {
                    case AstConstant _:
                        return Remove;
                    case AstSymbolRef _:
                        return Remove;
                    case AstThis _:
                        return Remove;
                    case AstWith _:
                        return node;
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

                        if (globalSymbol == "window")
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
                            if (unaryPrefix.Expression is AstCall call && call.Expression is AstFunction)
                                goto default;
                            node = unaryPrefix.Expression;
                            continue;
                        }

                        if (unaryPrefix.Operator == Operator.TypeOf ||
                            unaryPrefix.Operator == Operator.BitwiseNot || unaryPrefix.Operator == Operator.Void ||
                            unaryPrefix.Operator == Operator.Subtraction || unaryPrefix.Operator == Operator.Addition)
                        {
                            node = unaryPrefix.Expression;
                            continue;
                        }

                        goto default;
                    }
                    case AstFunction function:
                    {
                        if (function.Name == null || function.Name.Thedef!.OnlyDeclared)
                        {
                            return Remove;
                        }

                        return node;
                    }
                    case AstCall call:
                    {
                        var symbol = call.Expression.IsSymbolDef();
                        if (symbol != null && symbol.IsSingleInit && symbol.Init is AstLambda func &&
                            func.Pure == true || IsWellKnownPureFunction(call.Expression))
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
                        if (varDef.Name.IsSymbolDef()?.OnlyDeclared ?? false)
                        {
                            var value = varDef.Value;
                            if (value == null)
                                return Remove;
                            node = value;
                            continue;
                        }

                        return node;
                    }
                    default:
                        NeedValue = true;
                        node.Transform(this);
                        NeedValue = false;
                        return node;
                }
            }
        }

        AstNode MakeBlockFrom(AstNode from, params AstNode[] statements)
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

        static bool IsWellKnownPureFunction(AstNode callExpression)
        {
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
                "Object" => (propName switch
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
                }),
                "Math" => (propName switch
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
                }),
                _ => false
            };
        }

        static bool IsSideEffectFreePropertyAccess(string? globalSymbol, string? propName)
        {
            return globalSymbol switch
            {
                "Object" => (propName switch
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
                }),
                "Math" => (propName switch
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
                }),
                _ => false
            };
        }

        protected override AstNode? After(AstNode node, bool inList)
        {
            return null;
        }
    }
}
