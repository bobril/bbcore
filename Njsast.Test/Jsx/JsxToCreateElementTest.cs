using Njsast.Ast;
using Njsast.Jsx;
using Njsast.Output;
using Njsast.Reader;
using Xunit;

namespace Test.Jsx;

public class JsxToCreateElementTest
{
    [Fact]
    public void LowersIntrinsicTagWithBobrilFactory()
    {
        AssertLowered(
            "const view=<div id=\"root\" disabled>{title}</div>;",
            "const view=b.createElement(\"div\",{id:\"root\",disabled:true},title)",
            BobrilOptions());
    }

    [Fact]
    public void LowersMemberTagAndObjectSpreadProps()
    {
        AssertLowered(
            "const view=<Foo.Bar a=\"1\" {...props} b={n} />;",
            "const view=b.createElement(Foo.Bar,{a:\"1\",...props,b:n})",
            BobrilOptions());
    }

    [Fact]
    public void LowersLowercaseMemberTagAsExpression()
    {
        AssertLowered(
            "const view=<b.Component />;",
            "const view=b.createElement(b.Component,null)",
            BobrilOptions());
    }

    [Fact]
    public void LowersFragmentWithConfiguredFragmentSymbol()
    {
        AssertLowered(
            "const view=<>text<span />{...items}</>;",
            "const view=b.createElement(b.Fragment,null,\"text\",b.createElement(\"span\",null),...items)",
            BobrilOptions());
    }

    [Fact]
    public void UsesReactDefaults()
    {
        AssertLowered(
            "const view=<Component attr={<b />} />;",
            "const view=React.createElement(Component,{attr:React.createElement(\"b\",null)})",
            new JsxToCreateElementOptions());
    }

    static JsxToCreateElementOptions BobrilOptions()
    {
        return new()
        {
            Factory = "b.createElement",
            Fragment = "b.Fragment"
        };
    }

    static void AssertLowered(string source, string expected, JsxToCreateElementOptions options)
    {
        var toplevel = new Parser(new Options { EcmaVersion = 2022, ParseJSX = true }, source).Parse();
        var transformed = (AstToplevel)new JsxToCreateElementTreeTransformer(options).Transform(toplevel);
        Assert.Equal(expected, transformed.PrintToString(new OutputOptions { Ecma = 2022 }));
    }
}
