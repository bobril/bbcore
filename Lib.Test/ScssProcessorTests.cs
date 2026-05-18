using System;
using Lib.DiskCache;
using Lib.SCSSProcessor;
using Lib.Utils;
using Njsast.Css;
using Xunit;

namespace Lib.Test;

[CollectionDefinition("Serial", DisableParallelization = true)]
public class ScssProcessorTests
{
    readonly string _bbdir;
    readonly ToolsDir.ToolsDir _tools;

    public ScssProcessorTests()
    {
        _bbdir = PathUtils.Join(PathUtils.Normalize(Environment.CurrentDirectory), ".bbcore");
        _tools = new(PathUtils.Join(_bbdir, "tools"), new DummyLogger(), new NativeFsAbstraction());
    }

    [Fact]
    void ProcessSimpleScss()
    {
        var scssProcessor = new ScssProcessor(_tools);
        Assert.Equal(".c{width:100%}",
            scssProcessor.ProcessScss(".c { width: 100% }", "file:///dir/file.scss", url => url, url => url,
                Console.WriteLine).Result);
    }

    [Fact]
    void ProcessScssWithImport()
    {
        var scssProcessor = new ScssProcessor(_tools);
        Assert.Equal(".c{content:\"file:///global.scss\"}",
            scssProcessor.ProcessScss("@import '../global'; .c { content: '#{$prefix}' }", "file:///dir/file.scss",
                url => url + ".scss", url => "$prefix: '" + url + "';", Console.WriteLine).Result);
    }

    [Fact]
    void ProcessScssKeepsSafeNativeNesting()
    {
        var scssProcessor = new ScssProcessor(_tools);
        Assert.Equal(".c{width:100%;&:hover{color:red}.child{display:block}}",
            scssProcessor.ProcessScss(".c { width: 100%; &:hover { color: red } .child { display: block } }",
                "file:///dir/file.scss", url => url, url => url, Console.WriteLine).Result);
    }

    [Fact]
    void ProcessScssFlattensSassOnlySelectorConcatenation()
    {
        var scssProcessor = new ScssProcessor(_tools);
        Assert.Equal(".c{width:100%}.c__title{color:red}",
            scssProcessor.ProcessScss(".c { width: 100%; &__title { color: red } }", "file:///dir/file.scss",
                url => url, url => url, Console.WriteLine).Result);
    }

    [Fact]
    void ProcessScssToCssAst()
    {
        var scssProcessor = new ScssProcessor(_tools);
        var ast = scssProcessor.ProcessScssToCssAst("$width: 10px; .c { width: $width }", "file:///dir/file.scss",
            url => url, url => url, Console.WriteLine).Result;

        var rule = Assert.IsType<CssRule>(Assert.Single(ast.Nodes));
        Assert.Equal(".c", rule.Selector);
        var declaration = Assert.IsType<CssDeclaration>(Assert.Single(rule.Nodes));
        Assert.Equal("width", declaration.Property);
        Assert.Equal("10px", declaration.Value);
    }
}
