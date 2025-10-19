using System;
using Njsast.Ast;
using Njsast.Scope;

namespace Njsast;

public class SymbolDef : IEquatable<SymbolDef>
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
    public IPurpose? Purpose;

    public AstDestructuring? Destructuring;

    // let/const/var Name = VarInit. for var it is only for first declaration of var
    public AstNode? VarInit;
    internal int MangledIdx;

    public SymbolDef(AstScope scope, AstSymbol orig, AstNode? init)
    {
        Name = orig.Name;
        Scope = scope;
        Orig =
        [
            orig
        ];
        Init = init;
        References = new StructList<AstSymbol>();
        Global = false;
        MangledName = null;
        MangledIdx = -2;
        Undeclared = false;
        Defun = null;
    }

    public bool IsSingleInitAndDeeplyConst(bool forbidPropWrite = false)
    {
        if (Orig.Count != 1) return false;
        if (forbidPropWrite)
        {
            return References.All(sym =>
                !sym.Usage.HasFlag(SymbolUsage.Write) && !sym.Usage.HasFlag(SymbolUsage.PropWrite));
        }
        else
        {
            return References.All(sym => !sym.Usage.HasFlag(SymbolUsage.Write));
        }
    }

    public bool IsSingleInitAndDeeplyConstForbidDirectPropWrites()
    {
        if (Orig.Count != 1) return false;
        return References.All(sym =>
            !sym.Usage.HasFlag(SymbolUsage.Write) && !sym.Usage.HasFlag(SymbolUsage.PropWriteDirect));
    }

    public bool IsSingleInit
    {
        get
        {
            if (Orig.Count != 1) return false;
            return References.All(sym => !sym.Usage.HasFlag(SymbolUsage.Write));
        }
    }

    public bool OnlyDeclared => References.Count == 0 && !Scope.Pinned();

    public bool NeverRead =>
        References.All(s => s.Usage.HasFlag(SymbolUsage.Write) && !s.Usage.HasFlag(SymbolUsage.Read));

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
                             || options.KeepFunctionNames && orig is AstSymbolLambda or AstSymbolDefun
                             || orig is AstSymbolMethod
                             || options.KeepClassNames && orig is AstSymbolClass or AstSymbolDefClass;
        return UnmangleableCached.Value;
    }

    public void Mangle(ScopeOptions options)
    {
        if (MangledName != null) return;
        if (Unmangleable(options))
        {
            if (!options.IgnoreEval && Scope.Pinned())
            {
                var mangledIdx = AstScope.Debase54(options.Chars, Name);
                var enc = Scope.Enclosed.AsReadOnlySpan();
                foreach (var encSym in enc)
                {
                    if (encSym.MangledIdx == mangledIdx && encSym.MangledName != null)
                    {
                        encSym.MangledName = null;
                        encSym.Mangle(options);
                    }
                }
            }

            return;
        }

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

    public bool Equals(SymbolDef? other)
    {
        return ReferenceEquals(this, other);
    }
}