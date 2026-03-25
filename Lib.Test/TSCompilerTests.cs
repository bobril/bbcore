using Lib.DiskCache;
using Lib.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using Lib.TSCompiler;
using Lib.Composition;
using Lib.Watcher;

namespace Lib.Test;

[CollectionDefinition("Serial", DisableParallelization = true)]
public class CompilerTests
{
    string _bbdir;
    ToolsDir.ToolsDir _tools;
    CompilerPool _compilerPool;
    string projdir;

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
        Assert.Equal("5.6.3", _tools.TypeScriptVersion);
    }

    [Fact]
    public void TranspilerWorks()
    {
        var ts = _compilerPool.GetTs(null, new TSCompilerOptions { newLine = NewLineKind.LineFeed });
        var res = ts.Transpile("index.ts", "let a: string = 'ahoj';");
        Assert.Equal("var a = 'ahoj';\n", res.JavaScript);
        _compilerPool.ReleaseTs(ts);
    }

    [Fact]
    public void TypeScript6IgnoresDeprecatedCompilerOptionErrors()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bbcore-ts6-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "index.ts"), "export const value = 1;\n");
        var diskCache = new Lib.DiskCache.DiskCache(new NativeFsAbstraction(), () => new DummyWatcher());
        _tools.SetTypeScriptVersion("6.0.2");
        var ts = _compilerPool.GetTs(diskCache, new TSCompilerOptions
        {
            module = ModuleKind.Commonjs,
            sourceMap = true,
            target = ScriptTarget.Es2019
        });
        try
        {
            Assert.StartsWith("6.0", ts.GetTSVersion());
            var transpileResult = ts.Transpile("index.ts", "export const value = 1;\n");
            Assert.Contains("exports.value = void 0;", transpileResult.JavaScript);
            Assert.Contains("exports.value = 1;", transpileResult.JavaScript);
            Assert.DoesNotContain(transpileResult.Diagnostics ?? [], d => d.IsError && (d.Code == 5101 || d.Code == 5107));
            ts.CompilerOptions = new TSCompilerOptions
            {
                module = ModuleKind.Es2022,
                noEmit = true,
                target = ScriptTarget.Es2019,
                lib = new HashSet<string> { "es2022" }
            };
            ts.CheckProgram(tempDir, ["index.ts"]);
            var diags = ts.GetDiagnostics();
            Assert.DoesNotContain(diags, d => d.IsError && (d.Code == 5101 || d.Code == 5107));
        }
        finally
        {
            _compilerPool.ReleaseTs(ts);
            Directory.Delete(tempDir, true);
        }
    }
}
