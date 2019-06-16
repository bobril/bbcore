using Njsast.Ast;

namespace Njsast.ConstEval
{
    public interface IImportResolver
    {
        string ResolveName(JsModule module);
        string LoadContent(string fileName);
    }
}