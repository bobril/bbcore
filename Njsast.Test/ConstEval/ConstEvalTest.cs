using Njsast.Ast;
using Njsast.ConstEval;
using Njsast.Output;
using Njsast.Reader;
using Njsast.Runtime;
using Njsast.Scope;
using Xunit;

namespace Test.ConstEval;

public class ConstEvalTest
{
    [Theory]
    [ConstEvalDataProvider("Input/ConstEval")]
    public void ShouldCorrectlyEvaluateConstantsOrProduceSyntaxError(ConstEvalTestData testData)
    {
        var outNiceJs = ConstEvalTestCore(testData);

        Assert.Equal(testData.ExpectedNiceJs, outNiceJs);
    }

    public static string ConstEvalTestCore(ConstEvalTestData testData)
    {
        string outNiceJs;
        try
        {
            var files = new TestImportResolver();
            var ctx = new ResolvingConstEvalCtx(testData.InputFileName, files);
            var parser = new Parser(new Options(), testData.InputContent);
            var toplevel = parser.Parse();
            new ScopeParser().FigureOutScope(toplevel);
            var lastStatement = ((AstSimpleStatement) toplevel.Body.Last).Body;
            var val = lastStatement.ConstValue(ctx);
            outNiceJs = val != null ? "Const\n" : "Not const\n";
            if (val != null)
            {
                var valAst = TypeConverter.ToAst(val);
                var outputOptions = new OutputOptions {Beautify = true};
                outNiceJs += valAst.PrintToString(outputOptions);
            }
        }
        catch (SyntaxError e)
        {
            outNiceJs = e.Message;
        }

        return outNiceJs;
    }
}