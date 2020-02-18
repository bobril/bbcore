using System.Collections.Generic;
using Njsast.Ast;

namespace Njsast.Bundler
{
    class ToplevelRenameWalker : TreeWalker
    {
        readonly IDictionary<string, SymbolDef> _untouchables;
        readonly IReadOnlyDictionary<string, SymbolDef> _globals;
        readonly string _suffix;

        public ToplevelRenameWalker(IDictionary<string, SymbolDef> untouchables,
            IReadOnlyDictionary<string, SymbolDef> globals, string suffix)
        {
            _untouchables = untouchables;
            _globals = globals;
            _suffix = "_" + suffix;
        }

        protected override void Visit(AstNode node)
        {
            if (!(node is AstScope scope)) return;
            var haveToRename = scope is AstToplevel;
            if (!haveToRename)
            {
                foreach (var symbolDef in scope.Enclosed)
                {
                    if (!_untouchables.ContainsKey(symbolDef.Name) && !_globals.ContainsKey(symbolDef.Name)) continue;
                    haveToRename = true;
                    break;
                }
            }

            if (!haveToRename) return;
            var wasRename = false;
            foreach (var (name, symbol) in scope.Variables!)
            {
                if (!_untouchables.ContainsKey(name) && !_globals.ContainsKey(name)) continue;
                string newName;
                var index = 0;
                do
                {
                    index++;
                    newName = name + _suffix;
                    if (index > 1) newName += index.ToString();
                } while (_untouchables.ContainsKey(newName) || _globals.ContainsKey(newName));

                Helpers.RenameSymbol(symbol, newName);
                if (scope is AstToplevel)
                {
                    _untouchables[newName] = symbol;
                    wasRename = true;
                }
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
