using System.Collections.Generic;
using Njsast.Ast;

namespace Njsast.Bundler
{
    public class SourceFile
    {
        internal string Name;
        public AstToplevel Ast;
        public StructList<string> Requires = new StructList<string>();
        public StructList<string> LazyRequires = new StructList<string>();
        public AstSymbol? WholeExport;
        public StructList<SelfExport> SelfExports = new StructList<SelfExport>();
        public IDictionary<string, AstNode>? Exports = null;
        public StructList<string> PlainJsDependencies = new StructList<string>();
        public string? PartOfBundle;
        public StructList<string> NeedsWholeImportsFrom = new StructList<string>();
        public bool NeedsWholeExport;
        public bool OnlyWholeExport;
        /// list of file name and export name
        public StructList<(string, string)> NeedsImports = new StructList<(string, string)>();

        internal SourceFile(string name, AstToplevel ast)
        {
            Name = name;
            Ast = ast;
        }


        public void CreateWholeExport()
        {
            if (WholeExport != null) return;
            var wholeExportName = BundlerHelpers.MakeUniqueName("__export_$", Ast.Variables!, Ast.Globals!,
                "_" + BundlerHelpers.FileNameToIdent(Name));
            var init = new AstObject(Ast);
            foreach (var (propName, value) in Exports!)
            {
                init.Properties.Add(new AstObjectKeyVal(new AstString(propName), value));
            }

            var wholeExport = new AstSymbolVar(Ast, wholeExportName);
            var symbolDef = new SymbolDef(Ast, wholeExport, init);
            wholeExport.Thedef = symbolDef;
            var varDef = new AstVarDef(wholeExport, init);
            var astVar = new AstVar(Ast);
            astVar.Definitions.Add(varDef);
            Ast.Body.Add(astVar);
            Ast.Variables!.Add(wholeExportName, symbolDef);
            WholeExport = new AstSymbolRef(Ast, symbolDef, SymbolUsage.Unknown);
        }
    }

    public abstract class SelfExport
    {
    }

    class SimpleSelfExport : SelfExport
    {
        internal readonly string Name;
        internal readonly AstSymbol Symbol;

        internal SimpleSelfExport(string name, AstSymbol symbol)
        {
            Name = name;
            Symbol = symbol;
        }

        public override string ToString()
        {
            return $"{Name}: {Symbol.PrintToString()}";
        }
    }

    class ExportStarSelfExport : SelfExport
    {
        internal readonly string SourceName;

        internal ExportStarSelfExport(string sourceName)
        {
            SourceName = sourceName;
        }

        public override string ToString()
        {
            return $"*: {SourceName}";
        }
    }
}
