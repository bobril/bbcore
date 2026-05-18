using System.Collections.Generic;
using Njsast.Css;
using Xunit;

namespace Test.Css;

public class WptCssSyntaxTest
{
    // Cases are adapted from web-platform-tests/wpt css/css-syntax.
    // Source paths:
    // - css/css-syntax/at-rule-in-declaration-list.html
    // - css/css-syntax/var-with-blocks.html
    // - css/css-syntax/custom-property-rule-ambiguity.html
    // - css/css-syntax/missing-semicolon.html plus support/missing-semicolon.css
    public static IEnumerable<object[]> RoundTripCases()
    {
        yield return new object[]
        {
            "at-rule with block inside style rule",
            "div{@at{}color:green}",
            "div{@at{}color:green;}"
        };
        yield return new object[]
        {
            "at-rule with semicolon inside page rule",
            "@page{@at at;margin-top:20px}",
            "@page{@at at;margin-top:20px;}"
        };
        yield return new object[]
        {
            "font-face declaration after unknown at-rule",
            "@font-face{@at{}font-family:myfont}",
            "@font-face{@at{}font-family:myfont;}"
        };
        yield return new object[]
        {
            "missing semicolon at end of block",
            ".c{color:green}",
            ".c{color:green;}"
        };
        yield return new object[]
        {
            "whole-value block with var",
            ".a{color:{var(--x)};background-color:rgb(1, 1, 1)}",
            ".a{color:{var(--x)};background-color:rgb(1, 1, 1);}"
        };
        yield return new object[]
        {
            "whole-value block with spaces",
            ".a{color:{ var(--x) };background-color:rgb(1, 1, 1)}",
            ".a{color:{ var(--x) };background-color:rgb(1, 1, 1);}"
        };
        yield return new object[]
        {
            "custom property with trailing block",
            ".a{--y:var(--x) { };color:green}",
            ".a{--y:var(--x) { };color:green;}"
        };
        yield return new object[]
        {
            "custom property with leading block",
            ".a{--y:{ } var(--x);color:green}",
            ".a{--y:{ } var(--x);color:green;}"
        };
    }

    [Theory]
    [MemberData(nameof(RoundTripCases))]
    public void ParsesAndPrintsWptCssSyntaxCases(string name, string source, string expectedMinified)
    {
        var stylesheet = CssParser.Parse(source, new CssParserOptions { SourceFile = "wpt/" + name + ".css" });

        Assert.Equal(expectedMinified, stylesheet.PrintToString());
    }

    [Fact]
    public void ParsesWptVarWithBlocksCasesAsDeclarations()
    {
        var stylesheet = CssParser.Parse("""
            .a {
              --y:{ var(--x) } A;
              --z:A { var(--x) };
            }
            """);

        var rule = Assert.IsType<CssRule>(Assert.Single(stylesheet.Nodes));
        Assert.Collection(rule.Nodes,
            node =>
            {
                var declaration = Assert.IsType<CssDeclaration>(node);
                Assert.Equal("--y", declaration.Property);
                Assert.Equal("{ var(--x) } A", declaration.Value);
            },
            node =>
            {
                var declaration = Assert.IsType<CssDeclaration>(node);
                Assert.Equal("--z", declaration.Property);
                Assert.Equal("A { var(--x) }", declaration.Value);
            });
    }
}
