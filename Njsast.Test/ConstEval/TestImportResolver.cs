using System;
using System.IO;
using Njsast.Ast;
using Njsast.ConstEval;
using Njsast.Reader;
using Njsast.Utils;

namespace Test.ConstEval;

public class TestImportResolver : IImportResolver
{
    public (string fileName, AstToplevel content) ResolveAndLoad(JsModule module)
    {
        if (module.Name.StartsWith("./", StringComparison.Ordinal))
        {
            var fileName = PathUtils.Join(PathUtils.ParentSafe(module.ImportedFrom), module.Name);
            var input = File.ReadAllText(fileName + ".js");
            var parser = new Parser(new Options(), input);
            var toplevel = parser.Parse();
            toplevel.FigureOutScope();
            return (fileName, toplevel);
        }

        throw new NotSupportedException("TestImportResolver supports only relative paths " +
                                        module.ImportedFrom + " " + module.Name);
    }
}