using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Njsast.AstDump;
using Njsast.Bobril;
using Njsast.Compress;
using Njsast.Output;
using Njsast.Reader;
using Njsast.SourceMap;
using Xunit;

namespace Test.Compress;

public class CompressTest
{
    public static readonly ICompressOptions UnreachableCodeBlocksAndEmptyStatementsCompressOptions = new CompressOptions
    {
        EnableBlockElimination = true,
        EnableEmptyStatementElimination = true,
        EnableUnreachableCodeElimination = true,
        MaxPasses = 3
    };

    public static readonly ICompressOptions UnreachableCodeBlocksCompressOptions = new CompressOptions
    {
        EnableUnreachableCodeElimination = true,
        MaxPasses = 1
    };

    public static readonly ICompressOptions BlockEliminationCompressOptions = new CompressOptions
    {
        EnableBlockElimination = true,
        MaxPasses = 1
    };

    public static readonly ICompressOptions EmptyStatementEliminationCompressOptions = new CompressOptions
    {
        EnableEmptyStatementElimination = true,
        MaxPasses = 1
    };

    public static readonly ICompressOptions BooleanCompressCompressOptions = new CompressOptions
    {
        EnableBooleanCompress = true,
        MaxPasses = 1
    };

    public static readonly ICompressOptions FunctionReturnCompressCompressOptions = new CompressOptions
    {
        EnableFunctionReturnCompress = true,
        MaxPasses = 1
    };

    public static readonly ICompressOptions RemoveSideEffectFreeCodeCompressOptions = new CompressOptions
    {
        EnableRemoveSideEffectFreeCode = true,
        MaxPasses = 3
    };

    public static readonly ICompressOptions VariableHostingCompressOptions = new CompressOptions
    {
        EnableVariableHoisting = true,
        MaxPasses = 1
    };

    public static readonly ICompressOptions VariableHosting2PassesCompressOptions = new CompressOptions
    {
        EnableVariableHoisting = true,
        MaxPasses = 2
    };

    public static readonly ICompressOptions UnusedFunctionEliminationCompressOptions = new CompressOptions
    {
        EnableUnusedFunctionElimination = true,
        MaxPasses = 3
    };

    [Theory]
    [CompressDataProvider("Input/Compress/UnreachableCode/AnotherOptimizationsEnabled")]
    public void ShouldRemoveUnreachableCodeUnnecessaryBlocksAndEmptyStatements(CompressTestData testData)
    {
        RunAndAssert(testData, UnreachableCodeBlocksAndEmptyStatementsCompressOptions);
    }

    [Theory]
    [CompressDataProvider("Input/Compress/UnreachableCode/Only")]
    public void ShouldRemoveUnreachableCode(CompressTestData testData)
    {
        RunAndAssert(testData, UnreachableCodeBlocksCompressOptions);
    }

    [Theory]
    [CompressDataProvider("Input/Compress/RemoveBlock")]
    public void ShouldRemoveUnnecessaryBlocks(CompressTestData testData)
    {
        Assert.Equal(1u, BlockEliminationCompressOptions.MaxPasses);
        RunAndAssert(testData, BlockEliminationCompressOptions);
    }

    [Theory]
    [CompressDataProvider("Input/Compress/EmptyStatement")]
    public void ShouldRemoveEmptyStatements(CompressTestData testData)
    {
        RunAndAssert(testData, EmptyStatementEliminationCompressOptions);
    }

    [Theory]
    [CompressDataProvider("Input/Compress/BooleanCompress")]
    public void ShouldCompressBooleanExpressions(CompressTestData testData)
    {
        RunAndAssert(testData, BooleanCompressCompressOptions);
    }

    [Theory]
    [CompressDataProvider("Input/Compress/FunctionReturn")]
    public void ShouldCompressFunctionsReturnStatement(CompressTestData testData)
    {
        RunAndAssert(testData, FunctionReturnCompressCompressOptions);
    }

    [Theory]
    [CompressDataProvider("Input/Compress/VariableHoisting", searchSubDirectories: false)]
    public void ShouldHoistVariableToBeginOfScope(CompressTestData testData)
    {
        RunAndAssert(testData, VariableHostingCompressOptions);
    }

    [Theory]
    [CompressDataProvider("Input/Compress/VariableHoisting/2Passes")]
    public void ShouldHoistVariableToBeginOfScopeWith2Passes(CompressTestData testData)
    {
        RunAndAssert(testData, VariableHosting2PassesCompressOptions);
    }

    [Theory]
    [InlineData(
        "if(flag)var a=first(),b=second();else var c=third(),d=fourth();",
        "var a,b,c,d;if(flag){a=first();b=second()}else{c=third();d=fourth()}")]
    [InlineData(
        "var k;if(k){var n=call();if(null==n)k=\"\";else{if(null==n)n=4;else var r=1,n=r?2:3;}}",
        "var n,k,r;if(k){n=call();if(null==n)k=\"\";else{if(null==n)n=4;else{r=1;n=r?2:3}}}")]
    public async Task ParallelVariableHoistingKeepsConditionalAssignments(string input, string expected)
    {
        const int workers = 8;
        using var start = new Barrier(workers);
        var tasks = Enumerable.Range(0, workers).Select(_ => Task.Factory.StartNew(() =>
        {
            Assert.True(start.SignalAndWait(TimeSpan.FromSeconds(30)), "Workers did not start together.");
            for (var iteration = 0; iteration < 256; iteration++)
            {
                var data = new CompressTestData { InputFileName = "parallel.js", InputContent = input };
                var (_, actual, _) = CompressTestCore(data, VariableHosting2PassesCompressOptions);
                Assert.Equal(expected, actual);
            }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
        await Task.WhenAll(tasks);
    }

    [Theory]
    [CompressDataProvider("Input/Compress/UnusedFunction")]
    public void ShouldRemoveUnusedFunctions(CompressTestData testData)
    {
        RunAndAssert(testData, UnusedFunctionEliminationCompressOptions);
    }

    [Theory]
    [CompressDataProvider("Input/Compress/RemoveSideEffectFreeCode")]
    public void ShouldRemoveSideEffectFreeCode(CompressTestData testData)
    {
        RunAndAssert(testData, RemoveSideEffectFreeCodeCompressOptions);
    }

    [Fact]
    public void ShouldCompressOptionalChainOnKnownNullishValues()
    {
        var testData = new CompressTestData
        {
            InputFileName = "optional-chain-nullish.js",
            InputContent = "console.log(null?.x, undefined?.());"
        };

        var (_, outMinJs, outNiceJs) = CompressTestCore(testData, RemoveSideEffectFreeCodeCompressOptions);

        Assert.Equal("console.log(undefined,undefined)", outMinJs);
        Assert.Contains("console.log(undefined, undefined);", outNiceJs);
    }

    [Fact]
    public void ShouldCompressOptionalChainOnKnownNonNullishValues()
    {
        var testData = new CompressTestData
        {
            InputFileName = "optional-chain-non-nullish.js",
            InputContent = "console.log(({ x: 1 })?.x, \"abc\"?.length);"
        };

        var (_, outMinJs, outNiceJs) = CompressTestCore(testData, RemoveSideEffectFreeCodeCompressOptions);

        Assert.Equal("console.log(1,\"abc\".length)", outMinJs);
        Assert.Contains("console.log(1, \"abc\".length);", outNiceJs);
    }

    void RunAndAssert(CompressTestData testData, ICompressOptions options)
    {
        var (outAst, outMinJs, outNiceJs) = CompressTestCore(testData, options);

        Assert.Equal(testData.ExpectedAst, outAst);
        Assert.Equal(testData.ExpectedNiceJs, outNiceJs);
        Assert.Equal(testData.ExpectedMinJs, outMinJs);
    }

    public static (string outAst, string outMinJs, string outNiceJs)  CompressTestCore(CompressTestData testData, ICompressOptions options)
    {
        string outAst;
        var outNiceJs = string.Empty;
        var outMinJs = string.Empty;
        try
        {
            var commentListener = new CommentListener();
            var parser = new Parser(new ()
            {
                OnComment = (block, content, location) =>
                {
                    commentListener.OnComment(block, content, location);
                }
            }, testData.InputContent);
            var toplevel = parser.Parse();
            commentListener.Walk(toplevel);
            toplevel.FigureOutScope();
            toplevel = toplevel.Compress(options);
            var strSink = new StringLineSink();
            var dumper = new DumpAst(new AstDumpWriter(strSink));
            dumper.Walk(toplevel);
            outAst = strSink.ToString();
            var outMinJsBuilder = new SourceMapBuilder();
            toplevel.PrintToBuilder(outMinJsBuilder, new OutputOptions());
            outMinJs = outMinJsBuilder.Content();
            var outNiceJsBuilder = new SourceMapBuilder();
            toplevel.PrintToBuilder(outNiceJsBuilder, new OutputOptions {Beautify = true});
            outNiceJs = outNiceJsBuilder.Content();
        }
        catch (SyntaxError e)
        {
            outAst = e.Message;
        }

        return (outAst, outMinJs, outNiceJs);
    }
}
