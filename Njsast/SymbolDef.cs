using Njsast.Ast;
using Njsast.Scope;

namespace Njsast
{
    public class SymbolDef
    {
        public string Name;
        public string? MangledName;
        public StructList<AstSymbol> Orig;
        public AstNode? Init;
        public AstScope Scope;
        public StructList<AstSymbol> References;
        public bool Global;
        public bool Export;
        public bool Undeclared;
        public bool? UnmangleableCached;
        public AstScope? Defun;

        public AstDestructuring? Destructuring;

        // let/const/var Name = VarInit. for var it is only for first declaration of var
        public AstNode? VarInit;
        internal int MangledIdx;

        public SymbolDef(AstScope scope, AstSymbol orig, AstNode? init)
        {
            Name = orig.Name;
            Scope = scope;
            Orig = new StructList<AstSymbol>();
            Orig.Add(orig);
            Init = init;
            References = new StructList<AstSymbol>();
            Global = false;
            MangledName = null;
            MangledIdx = -2;
            Undeclared = false;
            Defun = null;
        }

        public bool IsSingleInit
        {
            get
            {
                if (Orig.Count != 1) return false;
                return References.All(sym => !sym.Usage.HasFlag(SymbolUsage.Write));
            }
        }

        public bool OnlyDeclared => References.Count == 0  && !Scope.Pinned();
        public bool NeverRead => References.All(s => s.Usage.HasFlag(SymbolUsage.Write) && !s.Usage.HasFlag(SymbolUsage.Read));

        public SymbolDef? Redefined()
        {
            return Defun?.Variables?.GetOrDefault(Name);
        }

        public bool Unmangleable(ScopeOptions options)
        {
            if (UnmangleableCached.HasValue) return UnmangleableCached.Value;
            var orig = Orig[0];
            UnmangleableCached = Global && !options.TopLevel
                   || Export
                   || Undeclared
                   || !options.IgnoreEval && Scope.Pinned()
                   || options.KeepFunctionNames && (orig is AstSymbolLambda || orig is AstSymbolDefun)
                   || orig is AstSymbolMethod
                   || options.KeepClassNames && (orig is AstSymbolClass || orig is AstSymbolDefClass);
            return UnmangleableCached.Value;
        }

        public void Mangle(ScopeOptions options)
        {
            if (MangledName != null || Unmangleable(options)) return;
            var def = Redefined();
            if (def != null)
            {
                if (def.MangledIdx >= 0)
                {
                    MangledName = def.MangledName;
                    MangledIdx = def.MangledIdx;
                }
                else
                {
                    MangledName = def.Name;
                    MangledIdx = AstScope.Debase54(options.Chars, MangledName);
                }
            }
            else
                (MangledName, MangledIdx) = ((string, int))Scope.NextMangled(options, this);
        }
    }
}
