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

    [Theory]
    [InlineData("function second(x){return x}",
        "var x_m2=1,x_m=2;function first(){return x_m2+x_m}function second(x){return x}")]
    [InlineData("console.log(x)",
        "var x=1,x_m=2;function first(){return x+x_m}console.log(x)")]
    public void MergerPreservesReferencesAndAvoidsExistingSuffixes(string source, string expected)
    {
        var main = Parser.Parse("var x=1,x_m=2;function first(){return x+x_m}");
        main.FigureOutScope();
        var second = Parser.Parse(source);
        second.FigureOutScope();
        BundlerHelpers.AppendToplevelWithRename(main, second, "second", []);
        Assert.Equal(expected, main.PrintToString());
        foreach (var (name, symbol) in main.Variables!)
            Assert.Equal(name, symbol.Name);
    }

    [Fact]
    public void MergerKeepsDeclaredGlobalsAcrossMultipleModules()
    {
        var main = Parser.Parse("var known=1;");
        main.FigureOutScope();
        for (var i = 0; i < 64; i++)
        {
            var next = Parser.Parse($"function f{i}(known){{return known}}console.log(known);");
            next.FigureOutScope();
            BundlerHelpers.AppendToplevelWithRename(main, next, "module" + i, ["known"]);
        }

        Assert.Equal("known", main.Variables!["known"].Name);
        Assert.DoesNotContain("known_m", main.PrintToString());
        main.FigureOutScope();
        Assert.Equal(64u, main.Variables["known"].References.Count);
    }
}
