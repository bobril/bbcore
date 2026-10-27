using System.Collections.Generic;
using Njsast.Ast;

namespace Njsast.Bundler;

class ToplevelRenameWalker : TreeWalker
{
    readonly IDictionary<string, SymbolDef> _variables;
    readonly HashSet<string> _nonRootSymbolNames;
    readonly string _suffix;
    readonly HashSet<string>? _onlyNames;
    readonly IReadOnlyDictionary<string, SymbolDef>? _allowedGlobals;
    readonly HashSet<string>? _knownDeclaredGlobals;

    public ToplevelRenameWalker(IDictionary<string, SymbolDef> variables,
        HashSet<string> nonRootSymbolNames, string suffix, HashSet<string>? onlyNames = null,
        IReadOnlyDictionary<string, SymbolDef>? allowedGlobals = null, HashSet<string>? knownDeclaredGlobals = null)
    {
        _onlyNames = onlyNames;
        _variables = variables;
        _nonRootSymbolNames = nonRootSymbolNames;
        _suffix = "_" + suffix;
        _allowedGlobals = allowedGlobals;
        _knownDeclaredGlobals = knownDeclaredGlobals;
    }

    bool IsForbidden(string name) => _nonRootSymbolNames.Contains(name) &&
                                     _allowedGlobals?.ContainsKey(name) != true &&
                                     _knownDeclaredGlobals?.Contains(name) != true;

    IEnumerable<KeyValuePair<string, SymbolDef>> Candidates(AstToplevel scope)
    {
        if (_onlyNames == null) return scope.Variables!;
        List<KeyValuePair<string, SymbolDef>> res = [];
        foreach (var name in _onlyNames)
        {
            if (scope.Variables!.TryGetValue(name, out var symbol)) res.Add(new(name, symbol));
        }
        return res;
    }

    protected override void Visit(AstNode node)
    {
        StopDescending();
        if (!(node is AstToplevel scope))
        {
            return;
        }
        List<KeyValuePair<string, SymbolDef>>? renamed = null;
        foreach (var (name, symbol) in Candidates(scope))
        {
            if (!IsForbidden(name) && !_variables.ContainsKey(name))
            {
                _variables[name] = symbol;
                continue;
            }
            string newName;
            var index = 0;
            do
            {
                index++;
                newName = name + _suffix;
                if (index > 1) newName += index.ToString();
            } while (IsForbidden(newName) || _variables.ContainsKey(newName) || scope.Variables!.ContainsKey(newName));

            Helpers.RenameSymbol(symbol, newName);
            _variables[newName] = symbol;
            (renamed ??= []).Add(new(name, symbol));
        }

        if (renamed == null) return;
        foreach (var (name, symbol) in renamed)
        {
            scope.Variables!.Remove(name);
            scope.Variables.Add(symbol.Name, symbol);
        }
    }
}
