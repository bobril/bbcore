using System.Collections.Generic;
using Njsast.Output;
using Njsast.Reader;
using Njsast.Scope;

namespace Njsast.Ast
{
    /// The toplevel scope
    public class AstToplevel : AstScope
    {
        /// [Object/S] a map of name -> SymbolDef for all undeclared names
        public Dictionary<string, SymbolDef> Globals;

        public AstToplevel(Parser parser, Position startPos, Position endPos) : base(parser, startPos, endPos)
        {
        }

        public SymbolDef DefGlobal(AstSymbol symbol)
        {
            var name = symbol.Name;
            if (Globals.ContainsKey(name))
            {
                return Globals[name];
            }

            var global = new SymbolDef(this, symbol, null);
            global.Undeclared = true;
            global.Global = true;
            Globals.Add(name, global);
            return global;
        }

        public override AstScope Resolve()
        {
            return this;
        }

        public override void CodeGen(OutputContext output)
        {
            if (HasUseStrictDirective)
            {
                output.Indent();
                output.PrintString("use strict");
                output.Print(";");
                output.Newline();
            }

            for (var i = 0u; i < Body.Count; i++)
            {
                var stmt = Body[i];
                if (!(stmt is AstEmptyStatement))
                {
                    output.Indent();
                    stmt.Print(output);
                    output.Newline();
                    output.Newline();
                }
            }
        }

        public override bool IsBlockScope => false;

        public void FigureOutScope(ScopeOptions options = null)
        {
            if (options == null) options = new ScopeOptions();
            new ScopeParser(options).FigureOutScope(this);
        }

        public void Mangle(ScopeOptions options = null)
        {
            if (options == null) options = new ScopeOptions();
            new ScopeParser(options).FigureOutScope(this);
            var m = new MangleTreeWalker(options);
            m.Mangle(this);
        }
    }
}
