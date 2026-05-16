using System.Collections.Generic;
using Njsast.Bundler;
using Njsast.Reader;
using Xunit;

namespace Test.Bundler;

public class BundlerHelpersTest
{
    [Fact]
    public void MergerUniques()
    {
        var main = new Parser(new(), "var a=1,b=2;").Parse();
        main.FigureOutScope();
        var second = new Parser(new(), "var a=3,c=a+1;").Parse();
        second.FigureOutScope();
        BundlerHelpers.AppendToplevelWithRename(main,second, "s", new());
        Assert.Equal("var a=1,b=2;var a_s=3,c=a_s+1", main.PrintToString());
    }
}
