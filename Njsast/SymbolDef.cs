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
        public int Eliminated;
        public AstScope Scope;
        public StructList<AstSymbol> References;
        public int Replaced;
        public bool Global;
        public bool Export;
        public bool Undeclared;
        public AstScope? Defun;

        public AstDestructuring? Destructuring;

        // let/const/var Name = VarInit. for var it is only for first declaration of var
        public AstNode? VarInit;

        public SymbolDef(AstScope scope, AstSymbol orig, AstNode? init)
        {
            Name = orig.Name;
            Scope = scope;
            Orig = new StructList<AstSymbol>();
            Orig.Add(orig);
            Init = init;
            Eliminated = 0;
            References = new StructList<AstSymbol>();
            Replaced = 0;
            Global = false;
            MangledName = null;
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

        public SymbolDef? Redefined()
        {
            return Defun?.Variables?.GetOrDefault(Name);
        }

        public bool Unmangleable(ScopeOptions options)
        {
            var orig = Orig[0];
            return Global && !options.TopLevel
                   || Export
                   || Undeclared
                   || !options.IgnoreEval && Scope.Pinned()
                   || (orig is AstSymbolLambda || orig is AstSymbolDefun) && options.KeepFunctionNames
                   || orig is AstSymbolMethod
                   || (orig is AstSymbolClass || orig is AstSymbolDefClass) && options.KeepClassNames;
        }

        public void Mangle(ScopeOptions options)
        {
            if (MangledName == null && !Unmangleable(options))
            {
                var def = Redefined();
                if (def != null)
                    MangledName = def.MangledName ?? def.Name;
                else
                    MangledName = Scope.NextMangled(options, this);
            }
        }
    }
}
