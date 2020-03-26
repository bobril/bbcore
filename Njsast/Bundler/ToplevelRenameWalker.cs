using System.Collections.Generic;
using Njsast.Ast;

namespace Njsast.Bundler
{
    class ToplevelRenameWalker : TreeWalker
    {
        readonly IDictionary<string, SymbolDef> _variables;
        readonly HashSet<string> _nonRootSymbolNames;
        readonly string _suffix;

        public ToplevelRenameWalker(IDictionary<string, SymbolDef> variables,
            HashSet<string> nonRootSymbolNames, string suffix)
        {
            _variables = variables;
            _nonRootSymbolNames = nonRootSymbolNames;
            _suffix = "_" + suffix;
        }

        protected override void Visit(AstNode node)
        {
            StopDescending();
            if (!(node is AstToplevel scope))
            {
                return;
            }
            var wasRename = false;
            foreach (var (name, symbol) in scope.Variables!)
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
                } while (_nonRootSymbolNames.Contains(newName) || _variables.ContainsKey(newName));

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
}
