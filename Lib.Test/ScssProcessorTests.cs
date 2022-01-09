using System;
using Lib.SCSSProcessor;
using Lib.Utils;
using Xunit;

namespace Lib.Test;

[CollectionDefinition("Serial", DisableParallelization = true)]
public class ScssProcessorTests
{
    string _bbdir;
    ToolsDir.ToolsDir _tools;

    public ScssProcessorTests()
    {
        _bbdir = PathUtils.Join(PathUtils.Normalize(Environment.CurrentDirectory), ".bbcore");
        _tools = new ToolsDir.ToolsDir(PathUtils.Join(_bbdir, "tools"), new DummyLogger());
    }

    [Fact]
    void ProcessSimpleScss()
    {
        var scssProcessor = new ScssProcessor(_tools);
        Assert.Equal(".c{width:100%}",
            scssProcessor.ProcessScss(".c { width: 100% }", "file:///dir/file.scss", url => url, Console.WriteLine).Result);
    }
}
