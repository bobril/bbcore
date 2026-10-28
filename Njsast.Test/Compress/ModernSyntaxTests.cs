using Njsast.Compress;
using Njsast.Output;
using Njsast.Reader;
using Xunit;

namespace Test.Compress;

public class ModernSyntaxTests
{
    static string Compress(string source, int ecma = 2021) => Parser.Parse(source)
        .Compress(new CompressOptions { MaxPasses = 1 }, null, ecma)
        .PrintToString(new OutputOptions { Ecma = ecma });

    [Theory]
    [InlineData("return x===null||x===void 0?y:x", "return x??y")]
    [InlineData("return null===x||void 0===x?y:x", "return x??y")]
    [InlineData("return x!==void 0&&x!==null?x:y", "return x??y")]
    [InlineData("return x===null||x===void 0?void 0:x.p", "return x?.p")]
    [InlineData("return x===null||x===void 0?void 0:x[y()]", "return x?.[y()]")]
    [InlineData("return x===null||x===void 0?void 0:x.p.q()", "return x?.p.q()")]
    [InlineData("return x===null||x===void 0?void 0:x(y)", "return x?.(y)")]
    [InlineData("return (x=y())===null||x===void 0?void 0:x.p", "return (x=y())?.p")]
    [InlineData("return (x=y())!==null&&x!==void 0?x:42", "return (x=y())??42")]
    [InlineData("return (x===null||x===void 0?void 0:x.p).q", "return (0,x?.p).q")]
    [InlineData("return (x===null||x===void 0?void 0:x.p)()", "return (0,x?.p)()")]
    [InlineData("return delete(x===null||x===void 0?void 0:x.p)", "return delete(0,x?.p)")]
    [InlineData("return x||(x=y)", "return x||=y")]
    [InlineData("return x&&(x=y)", "return x&&=y")]
    [InlineData("return x??(x=y)", "return x??=y")]
    [InlineData("return x=x||y", "return x||=y")]
    [InlineData("return x=x&&y", "return x&&=y")]
    [InlineData("return x=x??y", "return x??=y")]
    [InlineData("if(!x)x=y", "x||=y")]
    [InlineData("if(x){x=y}", "x&&=y")]
    [InlineData("if(x===null||x===void 0)x=y", "x??=y")]
    [InlineData("return x!==null&&x!==void 0?x:x=y", "return x??=y")]
    [InlineData("try{x()}catch(e){y()}", "try{x()}catch{y()}")]
    [InlineData("return this.p||(this.p=y)", "return this.p||=y")]
    [InlineData("const o=x;return o.p||(o.p=y)", "const o=x;return o.p||=y")]
    [InlineData("x!==null&&x!==void 0&&x.p()", "x?.p()")]
    [InlineData("x===null||x===void 0||x.p()", "x?.p()")]
    [InlineData("if(x!==null&&x!==void 0){x.p(y)}", "x?.p(y)")]
    [InlineData("return (x===null||x===void 0||x.p(),42)", "return x?.p(),42")]
    public void EmitsSmallerModernSyntax(string body, string expected)
    {
        var actual = Compress("function f(x,y){" + body + "}");
        Assert.Equal(Parser.Parse("function f(x,y){" + expected + "}").PrintToString(), actual);
        Parser.Parse(actual, new Options { EcmaVersion = 2021 });
    }

    [Theory]
    [InlineData("return x==null?void 0:x.p")]
    [InlineData("return x==null?y:x")]
    [InlineData("return x===null?void 0:x.p")]
    [InlineData("return x===void 0?void 0:x.p")]
    [InlineData("return x.p===null||x.p===void 0?void 0:x.p.q")]
    [InlineData("return x.p=x.p||y")]
    [InlineData("return x.p||(x.p=y)")]
    [InlineData("return x===null||x===void 0||x.p()")]
    [InlineData("return x!==null&&x!==void 0&&x.p()")]
    [InlineData("return x[y]||(x[y]=42)")]
    [InlineData("x=y;return x.p||(x.p=42)")]
    [InlineData("const c=1;return c=c||y")]
    [InlineData("try{x()}catch(e){return e}")]
    [InlineData("try{x()}catch({p}){y()}")]
    [InlineData("try{x()}catch(e){return eval('e')}")]
    [InlineData("with(y){return x||(x=42)}")]
    [InlineData("return x===null||x===void 0?void 0:x(...y,42)")]
    [InlineData("return x===null||x===void 0?void 0:new x()")]
    public void PreservesCasesWithDifferentSemantics(string body)
    {
        var source = "function f(x,y){" + body + "}";
        Assert.Equal(Parser.Parse(source).PrintToString(), Compress(source));
    }

    [Theory]
    [InlineData(5, "function f(x,y){return x===null||x===void 0?y:x}")]
    [InlineData(6, "function f(x,y){return x||(x=y)}")]
    [InlineData(2018, "function f(){try{g()}catch(e){h()}}")]
    [InlineData(2019, "function f(x){return x===null||x===void 0?void 0:x.p}")]
    [InlineData(2020, "function f(x,y){return x||(x=y)}")]
    public void HonorsOlderOutputTargets(int ecma, string source)
    {
        var actual = Compress(source, ecma);
        Assert.Equal(Parser.Parse(source).PrintToString(), actual);
        Parser.Parse(actual, new Options { EcmaVersion = ecma });
    }

    [Theory]
    [InlineData(10, "try{g()}catch(e){h()}", "try{g()}catch{h()}")]
    [InlineData(11, "return x===null||x===void 0?y:x", "return x??y")]
    [InlineData(12, "return x||(x=y)", "return x||=y")]
    public void AcceptsEditionNumbers(int ecma, string body, string expected) =>
        Assert.Equal(Parser.Parse("function f(x,y){" + expected + "}").PrintToString(),
            Compress("function f(x,y){" + body + "}", ecma));

    [Fact]
    public void ParenthesizesNullishMixedWithLogicalOperators()
    {
        var actual = Compress("function f(x,y){return (x!==null&&x!==void 0?x:y)||y}");
        Assert.Equal("function f(x,y){return(x??y)||y}", actual);
        Parser.Parse(actual);
    }

    [Fact]
    public void DoesNotTurnDirectEvalIntoIndirectEval()
    {
        var source = "function f(eval){return eval===null||eval===void 0?void 0:eval('x')}";
        Assert.Equal(Parser.Parse(source).PrintToString(), Compress(source));
    }

    [Fact]
    public void StrictParametersCannotChangeThroughArguments()
    {
        Assert.Equal("function f(x,y){\"use strict\";return x.p||=y}",
            Compress("function f(x,y){'use strict';return x.p||(x.p=y)}"));
    }
}
