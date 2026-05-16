using System;
using Njsast.Output;
using Njsast.Reader;
using Njsast.Utils;
using Test.ConstEval;
using Xunit;

namespace Test.Bundler;

public class ModuleParserTest
{
    [Theory]
    [ModuleParserDataProvider("Input/ModuleParser")]
    [ModuleParserDataProvider("Input/ModuleParser", "*.json")]
    public void ShouldCorrectlyEvaluateConstantsOrProduceSyntaxError(ConstEvalTestData testData)
    {
        var outNiceJs = ModuleParserTestCore(testData);

        Assert.Equal(testData.ExpectedNiceJs, outNiceJs);
    }

    public static string ModuleParserTestCore(ConstEvalTestData testData)
    {
        string outNiceJs;
        try
        {
            var sf = Njsast.Bundler.BundlerHelpers.BuildSourceFile(testData.InputFileName, testData.InputContent,
                null, Resolver);

            outNiceJs = sf.Ast!.PrintToString(new OutputOptions {Beautify = true});

            if (sf.Requires.Count > 0)
            {
                outNiceJs += "Requires:\n";
                foreach (var name in sf.Requires)
                {
                    outNiceJs += name + "\n";
                }
            }

            if (sf.LazyRequires.Count > 0)
            {
                outNiceJs += "Lazy requires:\n";
                foreach (var name in sf.LazyRequires)
                {
                    outNiceJs += name + "\n";
                }
            }

            if (sf.SelfExports.Count > 0)
            {
                outNiceJs += "Self exports:\n";
                foreach (var selfExport in sf.SelfExports)
                {
                    outNiceJs += selfExport + "\n";
                }
            }

            if (sf.Exports != null && sf.Exports.TryFindLongestPrefix(new ReadOnlySpan<string>(), out _, out var wholeExport))
            {
                outNiceJs += "Whole Export: " + wholeExport!.PrintToString() + "\n";
            }

            foreach (var import in sf.NeedsImports)
            {
                outNiceJs += "Uses " + import.File + " " + string.Join('.', import.Path) + "\n";
            }
        }
        catch (SyntaxError e)
        {
            outNiceJs = e.Message;
        }

        return outNiceJs;
    }

    static string Resolver(string from, string param)
    {
        return PathUtils.Join(PathUtils.Parent(from), param);
    }
}
