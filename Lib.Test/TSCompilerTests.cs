using Lib.DiskCache;
using Lib.Utils;
using System;
using Xunit;
using Lib.TSCompiler;
using Lib.Composition;

namespace Lib.Test;

[CollectionDefinition("Serial", DisableParallelization = true)]
public class CompilerTests
{
    string _bbdir;
    ToolsDir.ToolsDir _tools;
    CompilerPool _compilerPool;
    IFsAbstraction fs;
    string projdir;
    DiskCache.DiskCache dc;

    public CompilerTests()
    {
        _bbdir = PathUtils.Join(PathUtils.Normalize(Environment.CurrentDirectory), ".bbcore");
        _tools = new ToolsDir.ToolsDir(PathUtils.Join(_bbdir, "tools"), new DummyLogger(), new NativeFsAbstraction());
        _tools.SetTypeScriptVersion(ProjectOptions.DefaultTypeScriptVersion);
        _compilerPool = new CompilerPool(_tools, new DummyLogger());
    }

    [Fact]
    public void DefaultTypeScriptVersionDidntChanged()
    {
        Assert.Equal("5.2.2", _tools.TypeScriptVersion);
    }

    [Fact]
    public void TranspilerWorks()
    {
        var ts = _compilerPool.GetTs(null, new TSCompilerOptions { newLine = NewLineKind.LineFeed });
        var res = ts.Transpile("index.ts", "let a: string = 'ahoj';");
        Assert.Equal("var a = 'ahoj';\n", res.JavaScript);
        _compilerPool.ReleaseTs(ts);
    }
}
