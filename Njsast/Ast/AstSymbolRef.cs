using Njsast.ConstEval;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// Reference to some symbol (not definition/declaration)
    public class AstSymbolRef : AstSymbol
    {
        public AstSymbolRef(string? source, Position startLoc, Position endLoc, string name) : base(source, startLoc, endLoc, name)
        {
        }

        public AstSymbolRef(AstSymbol symbol) : base(symbol)
        {
        }

        public AstSymbolRef(string name) : base(name)
        {
        }

        public AstSymbolRef(AstNode from, string name) : base(from, name)
        {
        }

        public AstSymbolRef(AstNode from, SymbolDef def, SymbolUsage usage) : base(from, def.Name)
        {
            Thedef = def;
            def.References.Add(this);
            Usage = usage;
        }

        static bool IsVarLetConst(AstSymbol astSymbol)
        {
            var t = astSymbol.GetType();
            return t == typeof(AstSymbolVar) || t == typeof(AstSymbolLet) || t == typeof(AstSymbolConst);
        }

        public override AstNode ShallowClone()
        {
            return new AstSymbolRef(Source, Start, End, Name);
        }

        public override object? ConstValue(IConstEvalCtx? ctx = null)
        {
            if (Thedef == null) return null;
            if (Thedef.Global && Thedef.Undeclared)
            {
                if (Name == "Infinity") return AstInfinity.Instance;
                if (Name == "NaN") return AstNaN.Instance;
                if (Name == "undefined") return AstUndefined.Instance;
                return null;
            }

            if (Thedef.IsSingleInit)
            {
                if (Thedef.VarInit == null) return IsVarLetConst(Thedef.Orig[0]) ? AstUndefined.Instance : null;
                return Thedef.VarInit.ConstValue(ctx);
            }

            return null;
        }
    }
}
