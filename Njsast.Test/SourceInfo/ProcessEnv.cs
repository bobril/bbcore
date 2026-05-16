using System.Linq;
using Njsast.Bobril;
using Njsast.ConstEval;
using Njsast.Reader;
using Njsast.Utils;
using Xunit;

namespace Test.SourceInfo;

public class ProcessEnv
{
    [Fact]
    public void DetectsBasicUseCase()
    {
        var top = Parser.Parse("if (process.env.NODE_ENV === \"development\") console.log(\"debug\");");
        top.FigureOutScope();
        var files = new InMemoryImportResolver();
        var ctx = new ResolvingConstEvalCtx("src/a.js", files);
        var sourceInfo = GatherBobrilSourceInfo.Gather(top, ctx,
            (myctx, text) => PathUtils.Join(PathUtils.Parent(myctx.SourceName), text));
        var processEnv = sourceInfo.ProcessEnvs!.Single();
        Assert.Equal("NODE_ENV", processEnv.Name);
        Assert.Equal(0, processEnv.StartLine);
        Assert.Equal(4, processEnv.StartCol);
        Assert.Equal(0, processEnv.EndLine);
        Assert.Equal(24, processEnv.EndCol);
    }
}