using System;
using Lib.DiskCache;
using Lib.SCSSProcessor;
using Lib.Utils;
using Shared.DiskCache;
using Shared.Utils;
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
}
