using Lib.CSSProcessor;
using Lib.Utils;
using System;
using Xunit;

namespace Lib.Test;

[CollectionDefinition("Serial", DisableParallelization = true)]
public class CssProcessorTests
{
    string _bbdir;
    ToolsDir.ToolsDir _tools;

    public CssProcessorTests()
    {
        _bbdir = PathUtils.Join(PathUtils.Normalize(Environment.CurrentDirectory), ".bbcore");
        _tools = new ToolsDir.ToolsDir(PathUtils.Join(_bbdir, "tools"), new DummyLogger());
    }

    [Fact]
    void ProcessSimpleCss()
    {
        var cssProcessor = new CssProcessor(_tools);
        Assert.Equal(".c { width: 100% }", cssProcessor.ProcessCss(".c { width: 100% }", "/dir/file.css", (url, from) => url).Result);
    }

    [Fact]
    void ProcessCssWithUrl()
    {
        var cssProcessor = new CssProcessor(_tools);
        Func<string, string, string> urlReplacerUrlFrom = (url, from) =>
        {
            Assert.Equal("logo.png", url);
            Assert.Equal("./dir", from);
            return from + "/" + url;
        };
        Assert.Equal(".c { background-image: url(\"./dir/logo.png\") }",
            cssProcessor.ProcessCss(".c { background-image: url(\"logo.png\") }", "./dir/file.css",
                urlReplacerUrlFrom).Result);
    }

    [Fact]
    void MinimizeCss()
    {
        var cssProcessor = new CssProcessor(_tools);
        Assert.Equal(".c{width:100%}", cssProcessor.ConcatenateAndMinifyCss(
            new[] { new SourceFromPair(".c { width: 100% }", "/dir/file.css") }, (url, from) => url).Result);
    }

    [Fact]
    void ConcatenateAndMinimizeCss()
    {
        var cssProcessor = new CssProcessor(_tools);
        Assert.Equal(".c{background-image:url(/dir/logo.png)}.c2{background-image:url(pogo.png)}", cssProcessor.ConcatenateAndMinifyCss(
            new[] {
                new SourceFromPair(".c { background-image: url(\"logo.png\") }", "/dir/file.css"),
                new SourceFromPair(".c2 { background-image: url(\"pogo.png\") }", "./file.css")
            }, (url, from) => from+"/"+url).Result);
    }
}