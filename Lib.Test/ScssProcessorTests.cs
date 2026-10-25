using System;
using System.Threading.Tasks;
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
    async Task ProcessSimpleScss()
    {
        var scssProcessor = new ScssProcessor(_tools);
        Assert.Equal(".c{width:100%}",
            await scssProcessor.ProcessScss(".c { width: 100% }", "file:///dir/file.scss", url => url, url => url,
                Console.WriteLine));
    }

    [Fact]
    async Task ProcessScssWithImport()
    {
        var scssProcessor = new ScssProcessor(_tools);
        Assert.Equal(".c{content:\"file:///global.scss\"}",
            await scssProcessor.ProcessScss("@import '../global'; .c { content: '#{$prefix}' }", "file:///dir/file.scss",
                url => url + ".scss", url => "$prefix: '" + url + "';", Console.WriteLine));
    }

    [Fact]
    async Task ProcessScssKeepsSafeNativeNesting()
    {
        var scssProcessor = new ScssProcessor(_tools);
        Assert.Equal(".c{width:100%;&:hover{color:red}.child{display:block}}",
            await scssProcessor.ProcessScss(".c { width: 100%; &:hover { color: red } .child { display: block } }",
                "file:///dir/file.scss", url => url, url => url, Console.WriteLine));
    }

    [Fact]
    async Task ProcessScssFlattensSassOnlySelectorConcatenation()
    {
        var scssProcessor = new ScssProcessor(_tools);
        Assert.Equal(".c{width:100%}.c__title{color:red}",
            await scssProcessor.ProcessScss(".c { width: 100%; &__title { color: red } }", "file:///dir/file.scss",
                url => url, url => url, Console.WriteLine));
    }

    [Fact]
    async Task ProcessScssToCssAst()
    {
        var scssProcessor = new ScssProcessor(_tools);
        var ast = await scssProcessor.ProcessScssToCssAst("$width: 10px; .c { width: $width }", "file:///dir/file.scss",
            url => url, url => url, Console.WriteLine);

        var rule = Assert.IsType<CssRule>(Assert.Single(ast.Nodes));
        Assert.Equal(".c", rule.Selector);
        var declaration = Assert.IsType<CssDeclaration>(Assert.Single(rule.Nodes));
        Assert.Equal("width", declaration.Property);
        Assert.Equal("10px", declaration.Value);
    }
}
