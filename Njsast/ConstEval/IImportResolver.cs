using Njsast.Ast;

namespace Njsast.ConstEval
{
    public interface IImportResolver
    {
        (string? fileName, AstToplevel? content) ResolveAndLoad(JsModule module);
    }
}