using Njsast.Ast;
using Njsast.Reader;

namespace Njsast.Compress;

// Run after the ordinary compression passes: introducing an optional chain changes
// reference/short-circuit boundaries, which must survive until code generation.
public sealed class ModernSyntaxTreeTransformer(int ecmaVersion) : TreeTransformer
{
    readonly int _ecmaVersion = ecmaVersion >= 2015 ? ecmaVersion - 2009 : ecmaVersion;

    protected override AstNode? Before(AstNode node, bool inList) => node is AstWith ? node : null;

    protected override AstNode? After(AstNode node, bool inList)
    {
        if (_ecmaVersion >= 10 && node is AstCatch { Argname: AstSymbolCatch symbol } handler &&
            symbol.Thedef is { Orig.Count: 1, References.Count: 0 } definition && !definition.Scope.Pinned())
        {
            handler.Argname = null;
            Modified = true;
        }

        if (_ecmaVersion >= 11 && node is AstConditional conditional &&
            TryNullishGuard(conditional.Condition, out var value, out var reference, out var nullWhenTrue))
        {
            var fallback = nullWhenTrue ? conditional.Consequent : conditional.Alternative;
            var present = nullWhenTrue ? conditional.Alternative : conditional.Consequent;
            if (SameReference(reference, present))
            {
                var coalesce = new AstBinary(node.Source, node.Start, node.End, value, fallback,
                    Operator.NullishCoalescing);
                return _ecmaVersion >= 12 ? TryLogicalAssignment(coalesce) ?? coalesce : coalesce;
            }

            if (IsUndefined(fallback) && TryOptionalChain(present, reference, value))
                return PreserveValueBoundary(conditional, present);
        }

        if (_ecmaVersion >= 11 && node is AstBinary shortCircuit &&
            shortCircuit.Operator is Operator.LogicalAnd or Operator.LogicalOr && IsDiscardedValue(node) &&
            TryNullishGuard(shortCircuit.Left, out var guardedValue, out var guardedReference,
                out var guardWhenNull) && guardWhenNull == (shortCircuit.Operator == Operator.LogicalOr) &&
            TryOptionalChain(shortCircuit.Right, guardedReference, guardedValue))
            return shortCircuit.Right;

        if (_ecmaVersion >= 11 && node is AstIf { Alternative: null } guardedIf &&
            SingleExpression(guardedIf.Body) is { } expression &&
            TryNullishGuard(guardedIf.Condition, out var ifValue, out var ifReference, out var ifWhenNull) &&
            !ifWhenNull && TryOptionalChain(expression, ifReference, ifValue))
            return new AstSimpleStatement(expression);

        if (_ecmaVersion >= 12)
        {
            if (node is AstBinary binary && TryLogicalAssignment(binary) is { } assignment)
                return assignment;
            if (node is AstIf { Alternative: null } statement && SingleAssignment(statement.Body) is { } assign)
            {
                var condition = statement.Condition;
                var op = Operator.LogicalAndAssignment;
                if (condition is AstUnaryPrefix { Operator: Operator.LogicalNot } negation)
                {
                    condition = negation.Expression;
                    op = Operator.LogicalOrAssignment;
                }
                if (SameReference(condition, assign.Left) && IsStableAssignmentTarget(condition))
                    return new AstSimpleStatement(new AstAssign(assign.Left, assign.Right, op));
                if (TryNullishGuard(statement.Condition, out var assignedValue, out var assignedReference,
                        out var assignWhenNull) && assignWhenNull && assignedValue == assignedReference &&
                    SameReference(assignedReference, assign.Left))
                    return new AstSimpleStatement(new AstAssign(assign.Left, assign.Right,
                        Operator.NullishCoalescingAssignment));
            }
        }

        return null;
    }

    static AstAssign? SingleAssignment(AstNode body)
        => SingleExpression(body) is AstAssign { Operator: Operator.Assignment } assign ? assign : null;

    static AstNode? SingleExpression(AstNode body)
    {
        if (body is AstBlockStatement { Body.Count: 1 } block) body = block.Body[0];
        return body is AstSimpleStatement statement ? statement.Body : null;
    }

    bool IsDiscardedValue(AstNode node)
    {
        foreach (var parent in Parents())
        {
            if (parent is AstSequence sequence)
            {
                if (sequence.Expressions.Last != node) return true;
                node = parent;
                continue;
            }
            return parent is AstSimpleStatement || parent is AstUnaryPrefix { Operator: Operator.Void } ||
                   parent is AstFor loop && (loop.Init == node || loop.Step == node);
        }
        return false;
    }

    static AstNode? TryLogicalAssignment(AstBinary binary)
    {
        if (AssignmentOperator(binary.Operator) is { } op &&
            binary.Right is AstAssign { Operator: Operator.Assignment } assign &&
            SameReference(binary.Left, assign.Left) && IsStableAssignmentTarget(binary.Left))
            return new AstAssign(binary.Source, binary.Start, binary.End, assign.Left, assign.Right, op);

        // Only a mutable local can lose the otherwise unconditional write. A
        // property may have a setter, and assigning even the same value to const throws.
        if (binary is AstAssign { Operator: Operator.Assignment, Right: AstBinary right } &&
            AssignmentOperator(right.Operator) is { } assignmentOp &&
            binary.Left is AstSymbolRef symbol && IsStableLocal(symbol) &&
            symbol.Thedef!.Orig.All(declaration => declaration is AstSymbolVar or AstSymbolLet or
                AstSymbolFunarg or AstSymbolCatch) && SameReference(symbol, right.Left))
            return new AstAssign(binary.Source, binary.Start, binary.End, binary.Left, right.Right, assignmentOp);
        return null;
    }

    static Operator? AssignmentOperator(Operator op) => op switch
    {
        Operator.LogicalOr => Operator.LogicalOrAssignment,
        Operator.LogicalAnd => Operator.LogicalAndAssignment,
        Operator.NullishCoalescing => Operator.NullishCoalescingAssignment,
        _ => null
    };

    static bool IsStableLocal(AstSymbolRef symbol) =>
        symbol.Thedef is { Global: false, Undeclared: false } definition &&
        !definition.Scope.Pinned() && symbol.Scope?.Pinned() != true;

    static bool IsStableValue(AstNode node) =>
        node is AstSymbolRef symbol && IsStableLocal(symbol) || node is AstThis and not AstSuper;

    static bool IsStableObject(AstNode node)
    {
        if (node is AstThis and not AstSuper) return true;
        if (node is not AstSymbolRef symbol || !IsStableLocal(symbol) || !symbol.Thedef!.IsSingleInit)
            return false;
        // A sloppy function's parameter can change through arguments, and imported
        // live bindings can change in another module even without a local write.
        if (symbol.Thedef.Orig[0] is AstSymbolFunarg)
        {
            for (var scope = symbol.Thedef.Scope; scope != null; scope = scope.ParentScope)
                if (scope.HasUseStrictDirective) return true;
            return false;
        }
        return symbol.Thedef.Orig[0] is AstSymbolVar or AstSymbolLet or AstSymbolConst or AstSymbolCatch;
    }

    static bool IsStableAssignmentTarget(AstNode node) => node switch
    {
        AstSymbolRef symbol => IsStableLocal(symbol),
        AstDot { Optional: false } dot => IsStableObject(dot.Expression),
        AstSub { Optional: false, Property: AstString or AstNumber } sub => IsStableObject(sub.Expression),
        _ => false
    };

    static bool SameReference(AstNode left, AstNode right) => (left, right) switch
    {
        (AstSymbolRef a, AstSymbolRef b) => a.Thedef != null && a.Thedef == b.Thedef,
        (AstThis and not AstSuper, AstThis and not AstSuper) => true,
        (AstDot { Optional: false } a, AstDot { Optional: false } b) =>
            Equals(a.Property, b.Property) && SameReference(a.Expression, b.Expression),
        (AstSub { Optional: false } a, AstSub { Optional: false } b) =>
            SameReference(a.Expression, b.Expression) &&
            ((AstNode)a.Property).IsStructurallyEquivalentTo((AstNode)b.Property),
        _ => false
    };

    static bool IsUndefined(AstNode node) => node is AstUndefined ||
        node is AstUnaryPrefix { Operator: Operator.Void, Expression: AstConstant } ||
        node is AstSymbolRef { Name: "undefined", Thedef.Undeclared: true };

    static bool TryNullComparison(AstNode node, Operator op, out AstNode value, out bool isNull)
    {
        value = null!;
        isNull = false;
        if (node is not AstBinary comparison || comparison.Operator != op) return false;
        if (comparison.Left is AstNull || IsUndefined(comparison.Left))
        {
            value = comparison.Right;
            isNull = comparison.Left is AstNull;
            return true;
        }
        if (comparison.Right is AstNull || IsUndefined(comparison.Right))
        {
            value = comparison.Left;
            isNull = comparison.Right is AstNull;
            return true;
        }
        return false;
    }

    static bool TryNullishGuard(AstNode condition, out AstNode value, out AstNode reference,
        out bool nullWhenTrue)
    {
        value = reference = null!;
        nullWhenTrue = true;
        if (condition is not AstBinary guard || guard.Operator is not (Operator.LogicalOr or Operator.LogicalAnd))
            return false;
        nullWhenTrue = guard.Operator == Operator.LogicalOr;
        var op = nullWhenTrue ? Operator.StrictEquals : Operator.StrictNotEquals;
        // Loose == null checks also match document.all, unlike nullish coalescing
        // and optional chaining. Keep them.
        if (!TryNullComparison(guard.Left, op, out var first, out var firstNull) ||
            !TryNullComparison(guard.Right, op, out var second, out var secondNull) || firstNull == secondNull)
            return false;
        var repeated = first is AstAssign { Operator: Operator.Assignment } assign ? assign.Left : first;
        if (!IsStableValue(repeated) || !SameReference(repeated, second)) return false;
        value = first;
        reference = repeated;
        return true;
    }

    static bool TryOptionalChain(AstNode expression, AstNode reference, AstNode value)
    {
        switch (expression)
        {
            case AstPropAccess property:
                if (SameReference(property.Expression, reference))
                {
                    property.Expression = value;
                    property.Optional = true;
                    return true;
                }
                return TryOptionalChain(property.Expression, reference, value);
            case AstCall call when call is not AstNew:
                // Older Chrome implementations mishandle a non-final spread in an
                // optional chain. These calls can retain their original guard.
                for (var i = 0; i + 1 < call.Args.Count; i++)
                    if (call.Args[i] is AstExpansion) return false;
                if (SameReference(call.Expression, reference))
                {
                    // eval?.() is indirect eval, while eval() is direct eval.
                    if (reference is AstSymbolRef { Name: "eval" }) return false;
                    call.Expression = value;
                    call.Optional = true;
                    return true;
                }
                return TryOptionalChain(call.Expression, reference, value);
            default:
                return false;
        }
    }

    AstNode PreserveValueBoundary(AstConditional original, AstNode chain)
    {
        var parent = Parent();
        if (parent is AstPropAccess property && property.Expression == original ||
            parent is AstCall call && call.Expression == original ||
            parent is AstUnaryPrefix { Operator: Operator.Delete } ||
            parent is AstPrefixedTemplateString template && template.Prefix == original)
        {
            // A conditional produces a value, not a property reference. Keep the
            // receiver, delete semantics and the end of this optional chain intact.
            var sequence = new AstSequence(original);
            sequence.Expressions.Add(new AstNumber(0));
            sequence.Expressions.Add(chain);
            return sequence;
        }
        return chain;
    }
}
