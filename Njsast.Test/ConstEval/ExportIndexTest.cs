using Njsast.Ast;
using Njsast.ConstEval;
using Njsast.Output;
using Njsast.Reader;
using Xunit;

namespace Test.ConstEval;

public class ExportIndexTest
{
    const string Main = "/src/main.js";

    static InMemoryImportResolver Resolver() => new InMemoryImportResolver()
        .Add("/src/other", "exports.a = 7;\nexports.d = 8;\n")
        .Add("/src/chain", "__exportStar(require(\"./other\"), exports);\nexports.e = 9;\n")
        .Add("/src/whole", "module.exports = { a: 1, b: 2 };\n");

    [Theory]
    [InlineData("exports.a = 1;\nexports.b = \"x\";\nexports.a = 2;")]
    [InlineData("exports.a = exports.b = void 0;\nexports.a = 5;")]
    [InlineData("module.exports = { a: 1, b: 2 };")]
    [InlineData("exports.a = 1;\nmodule.exports = { a: 3 };\nexports.b = 4;")]
    [InlineData("__exportStar(require(\"./other\"), exports);\nexports.c = 3;")]
    [InlineData("exports.a = 1;\n__exportStar(require(\"./other\"), exports);")]
    [InlineData("__export(require(\"./chain\"));\nexports.b = 3;")]
    [InlineData("__exportStar(require(\"./missing\"), exports);\nexports.a = 1;")]
    [InlineData("(function () { exports.a = 1; exports.z = 2; })();\nexports.b = 3;")]
    [InlineData("exports.a.b = 1;\nvar x = exports.a;\nexports[\"b\"] = 2;\nmodule.exports.c = 3;")]
    [InlineData("exports.a = exports.b = exports.c = void 0;\nexports.a = 1;\nexports.d = 2;")]
    [InlineData("var exports = {};\nexports.a = 1;")]
    public void FindsSameExportAsWalkingWholeModule(string source)
    {
        foreach (var export in new[] { "a", "b", "c", "d", "e", "z", "missing" })
        {
            var expected = WithWalker(source, export);
            var actual = WithIndex(source, export);
            Assert.Equal(expected, actual);
        }
    }

    static string WithWalker(string source, string export)
    {
        var toplevel = Parse(source);
        var finder = new ExportFinder(export, new ResolvingConstEvalCtx(Main, Resolver()));
        finder.Walk(toplevel);
        return Print(finder.Result, finder.CompleteResult);
    }

    static string WithIndex(string source, string export)
    {
        var toplevel = Parse(source);
        // Asked twice, the second answer must not depend on the first one
        ExportIndex.For(toplevel).Find(export, new ResolvingConstEvalCtx(Main, Resolver()));
        var (result, complete) = ExportIndex.For(toplevel).Find(export, new ResolvingConstEvalCtx(Main, Resolver()));
        return Print(result, complete);
    }

    static AstToplevel Parse(string source)
    {
        var toplevel = new Parser(new Options(), source).Parse();
        toplevel.FigureOutScope();
        return toplevel;
    }

    static string Print(AstNode? result, bool complete) =>
        (result?.PrintToString(new OutputOptions { Beautify = true }) ?? "<none>") + " complete=" + complete;
}
