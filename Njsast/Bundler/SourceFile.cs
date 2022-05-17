using System;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using Njsast.Ast;

namespace Njsast.Bundler;

public class SourceFile
{
    internal string Name;
    public AstToplevel? Ast;
    public StructList<string> Requires = new StructList<string>();
    public StructList<string> LazyRequires = new StructList<string>();
    public StructList<SelfExport> SelfExports = new StructList<SelfExport>();
    public StringTrie<AstNode>? Exports = null;
    public StructList<string> PlainJsDependencies = new StructList<string>();
    public StructList<string> ExternalImports = new StructList<string>();
    public string? PartOfBundle;
    public bool OnlyWholeExport;
    public bool ExternalImport;

    /// list of file name and export name path (think namespaces). Empty array means need whole module imports as object.
    public StructList<(string, string[])> NeedsImports = new StructList<(string, string[])>();

    internal SourceFile(string name, AstToplevel ast)
    {
        Name = name;
        Ast = ast;
        ExternalImport = false;
    }

    internal SourceFile(string name)
    {
        Name = name;
        Ast = null;
        ExternalImport = true;
    }

    public void CreateWholeExport(string[] needPath)
    {
        if (Ast == null) throw new InvalidOperationException("ExternalImport cannot be used to create WholeExport");
        if (needPath.Length >= 1 && needPath[0] == "default" &&
            !Exports!.TryFindLongestPrefix(new[] {"default"}, out _, out _))
        {
            needPath = needPath.Skip(1).ToArray();
        }

        Exports!.EnsureKeyExists(needPath, tuples =>
        {
            var init = new AstObject(Ast!);
            foreach (var (propName, value) in tuples)
            {
                var valueRef = value;
                if (value is AstSymbolDeclaration decl)
                {
                    valueRef = new AstSymbolRef(value, decl.Thedef!, SymbolUsage.Read);
                }

                init.Properties.Add(new AstObjectKeyVal(new AstString(propName), valueRef));
            }

            var wholeExportName = BundlerHelpers.MakeUniqueName("__export_$", Ast.Variables!,
                Ast.CalcNonRootSymbolNames(),
                "_" + BundlerHelpers.FileNameToIdent(Name));
            var wholeExport = new AstSymbolVar(Ast, wholeExportName);
            var symbolDef = new SymbolDef(Ast, wholeExport, init);
            wholeExport.Thedef = symbolDef;
            var varDef = new AstVarDef(wholeExport, init);
            var astVar = new AstVar(Ast);
            astVar.Definitions.Add(varDef);
            Ast.Body.Add(astVar);
            Ast.Variables!.Add(wholeExportName, symbolDef);
            return new AstSymbolRef(Ast, symbolDef, SymbolUsage.Unknown);
        });
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
        return $"* from {SourceName}";
    }
}

class ExportAsNamespaceSelfExport : SelfExport
{
    internal readonly string SourceName;
    internal readonly string AsName;

    internal ExportAsNamespaceSelfExport(string sourceName, string asName)
    {
        SourceName = sourceName;
        AsName = asName;
    }

    public override string ToString()
    {
        return $"* as {AsName} from {SourceName}";
    }
}

class ReexportSelfExport : SelfExport
{
    internal readonly string SourceName;
    internal readonly string AsName;
    internal readonly string[] Path;

    internal ReexportSelfExport(string asName, string sourceName, string[] path)
    {
        AsName = asName;
        SourceName = sourceName;
        Path = path;
    }

    public override string ToString()
    {
        return $"{string.Join('.', Path)} as {AsName} from {SourceName}";
    }
}
