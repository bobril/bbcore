using System;
using System.Runtime.CompilerServices;
using Njsast.AstDump;
using Njsast.Output;
using Njsast.Reader;
using Njsast.Scope;

namespace Njsast.Ast
{
    /// Base class for all symbols
    public abstract class AstSymbol : AstNode
    {
        /// [AstScope/S] the current scope (not necessarily the definition scope)
        public AstScope? Scope;

        /// [string] name of this symbol
        public string Name;

        /// [SymbolDef/S] the definition of this symbol
        public SymbolDef? Thedef;

        public SymbolUsage Usage;

        protected AstSymbol(string? source, Position startLoc, Position endLoc, string name) : base(source, startLoc,
            endLoc)
        {
            Name = name;
        }

        protected AstSymbol(AstSymbol symbol) : base(symbol)
        {
            Name = symbol.Name;
        }

        protected AstSymbol(string name)
        {
            Name = name;
        }

        protected AstSymbol(AstNode from, string name) : base(from)
        {
            Name = name;
        }

        public override void DumpScalars(IAstDumpWriter writer)
        {
            base.DumpScalars(writer);
            writer.PrintProp("Name", Name);
            writer.PrintProp("Read", Usage.HasFlag(SymbolUsage.Read));
            writer.PrintProp("Write", Usage.HasFlag(SymbolUsage.Write));
        }

        public override void CodeGen(OutputContext output)
        {
            output.PrintName(Thedef?.MangledName ?? Thedef?.Name ?? Name);
        }

        public void MarkEnclosed(ScopeOptions options)
        {
            for (var s = Scope; s != null; s = s.ParentScope)
            {
                // Faster AddUnique
                var span = s.Enclosed.AsReadOnlySpan();
                var def = Thedef!;
                foreach (var symb in span)
                {
                    if (symb == def) goto alreadyExists;
                }
                s.Enclosed.Add(Thedef!);
                alreadyExists:
                if (options.KeepFunctionNames)
                {
                    foreach (var keyValuePair in s.Functions!)
                    {
                        Thedef!.Scope.Enclosed.AddUnique(keyValuePair.Value);
                    }
                }

                if (s == Thedef!.Scope) break;
            }
        }

        public void Reference(ScopeOptions options)
        {
            Thedef!.References.Add(this);
            MarkEnclosed(options);
        }
    }
}
