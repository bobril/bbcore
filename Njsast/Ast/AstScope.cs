using System;
using System.Collections.Generic;
using Njsast.AstDump;
using Njsast.Output;
using Njsast.Reader;
using Njsast.Scope;

namespace Njsast.Ast
{
    /// Base class for all statements introducing a lexical scope
    public class AstScope : AstBlock
    {
        /// [Object/S] a map of name -> SymbolDef for all variables/functions defined in this scope
        public Dictionary<string, SymbolDef>? Variables;

        /// [Object/S] like `variables`, but only lists function declarations
        public Dictionary<string, SymbolDef>? Functions;

        public bool HasUseStrictDirective;

        /// [boolean/S] tells whether this scope uses the `with` statement
        public bool UsesWith;

        /// [boolean/S] tells whether this scope contains a direct call to the global `eval`
        public bool UsesEval;

        /// [AstScope?/S] link to the parent scope
        public AstScope? ParentScope;

        /// [SymbolDef*/S] a list of all symbol definitions that are accessed from this scope or any subscopes
        public StructList<SymbolDef> Enclosed;

        /// [integer/S] current index for mangling variables (used internally by the mangler)
        public uint Cname;

        protected AstScope(string? source, Position startPos, Position endPos) : base(source, startPos, endPos)
        {
        }

        protected AstScope()
        {
        }

        public AstScope(AstNode from) : base(from)
        {
        }

        public override AstNode ShallowClone()
        {
            throw new InvalidOperationException("Cannot clone Scope");
        }

        public AstScope SetUseStrict(bool useStrict)
        {
            HasUseStrictDirective = useStrict;
            return this;
        }

        public override void DumpScalars(IAstDumpWriter writer)
        {
            base.DumpScalars(writer);
            writer.PrintProp("HasUseStrictDirective", HasUseStrictDirective);
        }

        public virtual void InitScopeVars(AstScope? parentScope)
        {
            Variables = new Dictionary<string, SymbolDef>();
            Functions = new Dictionary<string, SymbolDef>();
            UsesWith = false;
            UsesEval = false;
            ParentScope = parentScope;
            Enclosed = new StructList<SymbolDef>();
            Cname = 0;
        }

        public SymbolDef DefVariable(AstSymbol symbol, AstNode? init)
        {
            SymbolDef def;
            if (Variables!.ContainsKey(symbol.Name))
            {
                def = Variables[symbol.Name];
                def.Orig.Add(symbol);
                if (def.Init != null && (!ReferenceEquals(def.Scope, symbol.Scope) || def.Init is AstFunction))
                {
                    def.Init = init;
                }
            }
            else
            {
                def = new SymbolDef(this, symbol, init);
                Variables.Add(symbol.Name, def);
                def.Global = ParentScope == null;
            }

            return symbol.Thedef = def;
        }

        public SymbolDef DefFunction(AstSymbol symbol, AstNode? init)
        {
            var def = DefVariable(symbol, init);
            if (def.Init == null || def.Init is AstDefun) def.Init = init;
            if (!Functions!.ContainsKey(symbol.Name))
                Functions.Add(symbol.Name, def);
            return def;
        }

        public SymbolDef? FindVariable(AstSymbol symbol)
        {
            return FindVariable(symbol.Name);
        }

        public SymbolDef? FindVariable(string name)
        {
            return Variables!.ContainsKey(name) ? Variables[name] : ParentScope?.FindVariable(name);
        }

        public virtual AstScope? Resolve()
        {
            return ParentScope;
        }

        public AstScope DefunScope()
        {
            var self = this;
            while (self.IsBlockScope)
            {
                self = self.ParentScope!;
            }

            return self;
        }

        public bool Pinned()
        {
            // It is not possible to mangle variables when there is eval or with.
            return UsesEval || UsesWith;
        }

        public static string Base54(ReadOnlySpan<char> chars, uint idx)
        {
            Span<char> buf = stackalloc char[8];
            buf[0] = chars[(int) (idx % 54)];
            idx = idx / 54;
            var resIdx = 1;

            while (idx > 0)
            {
                idx--;
                buf[resIdx++] = chars[(int) (idx % 64)];
                idx = idx / 64;
            }

            return new string(buf.Slice(0, resIdx));
        }

        public virtual string NextMangled(ScopeOptions options, SymbolDef symbolDef)
        {
            var ext = Enclosed;
            while (true)
            {
                again:
                var m = Base54(options.Chars, Cname++);
                if (!OutputContext.IsIdentifier(m)) continue; // skip over "do"

                // https://github.com/mishoo/UglifyJS2/issues/242 -- do not
                // shadow a name reserved from mangling.
                if (options.Reserved.Contains(m)) continue;

                // we must ensure that the mangled name does not shadow a name
                // from some parent scope that is referenced in this or in
                // inner scopes.
                for (var i = 0u; i < ext.Count; i++)
                {
                    var sym = ext[i];
                    var name = sym.MangledName ?? (sym.Unmangleable(options) ? sym.Name : null);
                    if (m == name) goto again;
                }

                return m;
            }
        }
    }
}
