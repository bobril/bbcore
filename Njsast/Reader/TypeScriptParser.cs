using Njsast;
using Njsast.Ast;

namespace Njsast.Reader;

public static class TypeScriptParser
{
    public static AstToplevel Parse(string input, Options? options = null)
    {
        options ??= new Options();
        options.ParseTypeScript = true;
        return EraseTypeScriptOnly(Parser.Parse(input, options));
    }

    public static AstToplevel ParseTsx(string input, Options? options = null)
    {
        options ??= new Options();
        options.ParseTypeScript = true;
        options.ParseJSX = true;
        return EraseTypeScriptOnly(Parser.Parse(input, options));
    }

    static AstToplevel EraseTypeScriptOnly(AstToplevel ast)
    {
        return (AstToplevel)new TypeScriptEraseTransformer().Transform(ast);
    }

    sealed class TypeScriptEraseTransformer : TreeTransformer
    {
        protected override AstNode? Before(AstNode node, bool inList)
        {
            return node is AstTypeScriptOnly
                ? inList ? Remove : new AstEmptyStatement(node.Source, node.Start, node.End)
                : null;
        }

        protected override AstNode? After(AstNode node, bool inList)
        {
            return null;
        }
    }
}
