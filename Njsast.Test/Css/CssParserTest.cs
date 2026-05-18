using System.Linq;
using Njsast.Css;
using Njsast.SourceMap;
using Xunit;

namespace Test.Css;

public class CssParserTest
{
    [Fact]
    public void TokenizerReadsBasicCssTokens()
    {
        var tokens = new CssTokenizer("@media screen { .a { color: red } }").Tokenize().ToArray();

        Assert.Contains(tokens, t => t.Type == CssTokenType.AtKeyword && t.Text == "@media");
        Assert.Contains(tokens, t => t.Type == CssTokenType.Identifier && t.Text == "color");
        Assert.Equal(CssTokenType.EndOfFile, tokens[^1].Type);
    }

    [Fact]
    public void PrintsBeautifiedCss()
    {
        var stylesheet = CssParser.Parse(".a{color:red;background:url(\"a.png\")}@media screen{.b{display:grid}}");

        Assert.Equal("""
            .a {
                color: red;
                background: url("a.png");
            }
            @media screen {
                .b {
                    display: grid;
                }
            }

            """.Replace("\r\n", "\n"), stylesheet.PrintToString(new CssOutputOptions { Beautify = true }));
    }

    [Fact]
    public void PrintsMinifiedCssWithoutComments()
    {
        var stylesheet = CssParser.Parse("/* x */ .a { color: red; }");

        Assert.Equal(".a{color:red;}", stylesheet.PrintToString(new CssOutputOptions { PreserveComments = false }));
    }

    [Fact]
    public void ConcatenatesStylesheetsInOrder()
    {
        var first = CssParser.Parse(".a{color:red}", new CssParserOptions { SourceFile = "a.css" });
        var second = CssParser.Parse(".b{color:blue}", new CssParserOptions { SourceFile = "b.css" });

        Assert.Equal(".a{color:red;}.b{color:blue;}", CssStylesheet.Concat(new[] { first, second }).PrintToString());
    }

    [Fact]
    public void RewritesUrlsButKeepsDataUrls()
    {
        var stylesheet = CssParser.Parse(".a{background:url(\"img/a.png\");mask:url(data:image/png;base64,abc)}",
            new CssParserOptions { SourceFile = "/src/a.css" });

        CssUrlRewriter.Rewrite(stylesheet, (url, from) => from + ":" + url);

        Assert.Equal(".a{background:url(\"/src/a.css:img/a.png\");mask:url(data:image/png;base64,abc);}",
            stylesheet.PrintToString());
    }

    [Fact]
    public void PrintToBuilderCreatesSourceMap()
    {
        var stylesheet = CssParser.Parse(".a{color:red}", new CssParserOptions { SourceFile = "a.css" });
        var builder = new SourceMapBuilder();

        stylesheet.PrintToBuilder(builder);
        var map = builder.Build(".", ".");

        Assert.Equal(".a{color:red;}", builder.Content());
        Assert.Contains("a.css", map.sources);
        Assert.NotEmpty(map.mappings);
    }

    [Fact]
    public void MinifierAppliesSafeAstLevelOptimizations()
    {
        var stylesheet = CssParser.Parse("""
            /* remove */
            .a > .b, .c + .d {
                margin: 0px 10.5000px 0.50em 00.0ms;
                color: #aabbcc;
                background: white;
                --custom: 0px 10.5000px #aabbcc;
            }
            .empty {}
            @media (min-width:  000.5000px) { .x { color: transparent } }
            """);

        CssMinifier.Minify(stylesheet);

        Assert.Equal(
            ".a>.b,.c+.d{margin:0 10.5px .5em 0;color:#abc;background:#fff;--custom:0px 10.5000px #aabbcc;}@media (min-width:.5px){.x{color:#0000;}}",
            stylesheet.PrintToString(new CssOutputOptions { PreserveComments = false }));
    }
}
