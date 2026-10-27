using System.Collections.Generic;
using Njsast.Ast;

namespace Njsast.Compress;

// References inside an unreachable function cannot keep another function alive.
// Resolve the whole dependency graph so chains and mutually recursive groups do
// not require one full compression/scope-analysis pass per declaration.
sealed class UnusedFunctionReachability : TreeWalker
{
    sealed class Function(AstLambda node, SymbolDef definition)
    {
        internal readonly AstLambda Node = node;
        internal readonly SymbolDef Definition = definition;
        internal readonly List<Function> Dependencies = [];
        internal bool Live;
    }

    readonly Dictionary<AstScope, Function> _functions = [];
    readonly Dictionary<AstScope, Function?> _owners = [];

    internal static HashSet<AstLambda> Find(AstToplevel top)
    {
        var walker = new UnusedFunctionReachability();
        walker.Walk(top);
        Stack<Function> pending = [];
        foreach (var function in walker._functions.Values)
        {
            foreach (var reference in function.Definition.References)
            {
                // A declaration can be recorded as a write when a reference appeared
                // before its initializer (including a forward reference in a closure).
                if (reference == function.Definition.Orig[0]) continue;
                var owner = walker.FindOwner(reference.Scope);
                if (owner == null)
                {
                    if (!function.Live)
                    {
                        function.Live = true;
                        pending.Push(function);
                    }
                }
                else if (owner != function)
                {
                    owner.Dependencies.Add(function);
                }
            }
        }

        while (pending.TryPop(out var function))
        {
            foreach (var dependency in function.Dependencies)
            {
                if (dependency.Live) continue;
                dependency.Live = true;
                pending.Push(dependency);
            }
        }

        HashSet<AstLambda> unused = [];
        foreach (var function in walker._functions.Values)
            if (!function.Live) unused.Add(function.Node);
        return unused;
    }

    Function? FindOwner(AstScope? scope)
    {
        if (scope == null) return null;
        if (_functions.TryGetValue(scope, out var function)) return function;
        if (_owners.TryGetValue(scope, out function)) return function;
        function = FindOwner(scope.ParentScope);
        _owners.Add(scope, function);
        return function;
    }

    protected override void Visit(AstNode node)
    {
        // Purpose detection can leave a removal marker for the following transform.
        if (TreeTransformer.IsRemove(node))
        {
            StopDescending();
            return;
        }
        AstLambda? function = null;
        SymbolDef? definition = null;
        if (node is AstDefun declaration)
        {
            function = declaration;
            definition = declaration.Name?.Thedef;
        }
        else if (node is AstVarDef { Value: AstLambda initializer } variable &&
                 Parent(1) is not AstFor and not AstForIn &&
                 variable.Name.IsSymbolDef() is { } variableDefinition)
        {
            function = initializer;
            definition = variableDefinition;
        }

        if (function != null && definition is { Global: false, Export: false } &&
            !definition.Scope.Pinned() && definition.Orig.Count == 1 &&
            definition.References.All(reference => reference == definition.Orig[0] ||
                                                   !reference.Usage.HasFlag(SymbolUsage.Write)))
        {
            _functions.Add(function, new Function(function, definition));
        }
    }
}
