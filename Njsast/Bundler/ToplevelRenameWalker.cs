using System.Collections.Generic;
using Njsast.Ast;

namespace Njsast.Bundler;

class ToplevelRenameWalker : TreeWalker
{
    readonly IDictionary<string, SymbolDef> _variables;
    readonly HashSet<string> _nonRootSymbolNames;
    readonly string _suffix;
    readonly HashSet<string>? _onlyNames;

    public ToplevelRenameWalker(IDictionary<string, SymbolDef> variables,
        HashSet<string> nonRootSymbolNames, string suffix, HashSet<string>? onlyNames = null)
    {
        _onlyNames = onlyNames;
        _variables = variables;
        _nonRootSymbolNames = nonRootSymbolNames;
        _suffix = "_" + suffix;
    }

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
        var wasRename = false;
        foreach (var (name, symbol) in Candidates(scope))
        {
            if (!_nonRootSymbolNames.Contains(name) && !_variables.ContainsKey(name))
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
            } while (_nonRootSymbolNames.Contains(newName) || _variables.ContainsKey(newName) || scope.Variables!.ContainsKey(newName));

            Helpers.RenameSymbol(symbol, newName);
            _variables[newName] = symbol;
            wasRename = true;
        }

        while (wasRename)
        {
            wasRename = false;
            foreach (var (name, symbol) in scope.Variables!)
            {
                if (name != symbol.Name)
                {
                    scope.Variables!.Remove(name);
                    scope.Variables!.Add(symbol.Name, symbol);
                    wasRename = true;
                    break;
                }
            }
        }
    }
}