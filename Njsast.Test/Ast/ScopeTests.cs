using System.Linq;
using Njsast.Ast;
using Njsast.Reader;
using Njsast.Scope;
using Xunit;

namespace Test.Ast;

public class ScopeTests
{
    [Theory]
    [InlineData(4, false)]
    [InlineData(32, false)]
    [InlineData(128, false)]
    [InlineData(128, true)]
    public void RepeatedScopeAnalysisPreservesCapturedDefinitions(int count, bool keepFunctionNames)
    {
        var names = Enumerable.Range(0, count).Select(i => "v" + i).ToArray();
        var expression = string.Join("+", names);
        var top = Parser.Parse("var " + string.Join(",", names.Select(n => n + "=1")) +
                               ";function outer(){function inner(){return " + expression + "+" + expression +
                               ";}return inner();}outer();");
        var options = new ScopeOptions { KeepFunctionNames = keepFunctionNames };
        for (var pass = 0; pass < 3; pass++)
        {
            top.FigureOutScope(options);
            var outer = Assert.IsType<AstDefun>(top.Body[1]);
            var inner = Assert.IsType<AstDefun>(outer.Body[0]);
            foreach (var scope in new AstScope[] { top, outer, inner })
            {
                var enclosed = scope.Enclosed.ToArray();
                Assert.Equal(enclosed.Length, enclosed.Distinct().Count());
                Assert.Equal(names, enclosed.Where(s => names.Contains(s.Name)).Select(s => s.Name));
                foreach (var name in names)
                    Assert.Contains(top.Variables![name], enclosed);
            }
        }
    }

    [Fact]
    public void EnclosedDefinitionsDistinguishShadowedNames()
    {
        var names = Enumerable.Range(0, 64).Select(i => "v" + i).ToArray();
        var expression = string.Join("+", names);
        var top = Parser.Parse("var " + string.Join(",", names.Select(n => n + "=1")) +
                               ";function outer(){" + expression +
                               ";{let v0=2;function inner(){return " + expression + ";}inner();}}");
        top.FigureOutScope();
        var outer = Assert.IsType<AstDefun>(top.Body[1]);
        var block = Assert.IsType<AstBlockStatement>(outer.Body[1]);
        var inner = Assert.IsType<AstDefun>(block.Body[1]);
        var global = top.Variables!["v0"];
        var local = block.BlockScope!.Variables!["v0"];
        Assert.NotSame(global, local);
        Assert.Contains(global, outer.Enclosed);
        Assert.Contains(local, inner.Enclosed);
        Assert.DoesNotContain(global, inner.Enclosed);
    }
}
