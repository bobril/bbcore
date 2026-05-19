using Njsast;
using Njsast.Ast;

namespace Njsast.Reader;

public static class TypeScriptParser
{
    public static AstToplevel Parse(string input, Options? options = null)
    {
        options ??= new Options();
        options.ParseTypeScript = true;
        return EraseTypeScriptOnly(Parser.Parse(input, options), options);
    }

    public static AstToplevel ParseTsx(string input, Options? options = null)
    {
        options ??= new Options();
        options.ParseTypeScript = true;
        options.ParseJSX = true;
        return EraseTypeScriptOnly(Parser.Parse(input, options), options);
    }

    static AstToplevel EraseTypeScriptOnly(AstToplevel ast, Options options)
    {
        if (options.ParsedTypeScriptImportEquals)
            ast.FigureOutScope();
        return (AstToplevel)new TypeScriptEraseTransformer().Transform(ast);
    }

    sealed class TypeScriptEraseTransformer : TreeTransformer
    {
        protected override AstNode? Before(AstNode node, bool inList)
        {
            return node is AstTypeScriptOnly
                ? inList ? Remove : new AstEmptyStatement(node.Source, node.Start, node.End)
                : node is AstTypeScriptImportEquals importEquals && IsUnusedImportEquals(importEquals)
                    ? inList ? Remove : new AstEmptyStatement(node.Source, node.Start, node.End)
                : node is AstTypeScriptImportEqualsConst constImportEquals && IsUnusedImportEquals(constImportEquals)
                    ? inList ? Remove : new AstEmptyStatement(node.Source, node.Start, node.End)
                : null;
        }

        protected override AstNode? After(AstNode node, bool inList)
        {
            return null;
        }

        static bool IsUnusedImportEquals(AstDefinitions importEquals)
        {
            foreach (var definition in importEquals.Definitions.AsReadOnlySpan())
                if (definition.Name is AstSymbol { Thedef.References.Count: > 0 })
                    return false;
            return true;
        }
    }
}
