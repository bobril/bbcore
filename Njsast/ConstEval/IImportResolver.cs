using Njsast.Ast;

namespace Njsast.ConstEval
{
    public interface IImportResolver
    {
        (string fileName, string content) ResolveAndLoad(JsModule module);
    }
}