using Lib.DiskCache;
using Lib.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using Lib.TSCompiler;
using Lib.Composition;
using Lib.Watcher;
using Njsast.Bobril;
using Njsast.ConstEval;
using Njsast.Reader;

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

    [Fact]
    public void TypeScript6StillFindsBobrilSpritesInGatheredSourceInfo()
    {
        _tools.SetTypeScriptVersion("6.0.2");
        var ts = _compilerPool.GetTs(null, new TSCompilerOptions
        {
            jsx = JsxEmit.React,
            module = ModuleKind.Commonjs,
            reactNamespace = "b",
            resolveJsonModule = true,
            sourceMap = true,
            target = ScriptTarget.Es2019
        });
        try
        {
            var input = """
                        import * as b from "bobril";

                        let icon1 = b.sprite("gradient.svg", "blue");
                        let icon2 = b.sprite("gradient2.svg");
                        let icon3 = b.sprite("light.png", undefined);
                        """;
            var transpileResult = ts.Transpile("index.ts", input);
            var parser = new Parser(new Options(), transpileResult.JavaScript);
            var toplevel = parser.Parse();
            toplevel.FigureOutScope();
            var sourceInfo = GatherBobrilSourceInfo.Gather(
                toplevel,
                new ResolvingConstEvalCtx("/tmp/index.ts", null!),
                static (ctx, text) => text
            );
            Assert.Equal("b", sourceInfo.BobrilImport);
            Assert.NotNull(sourceInfo.Imports);
            Assert.Contains(sourceInfo.Imports!, import => import.Name == "bobril");
            Assert.NotNull(sourceInfo.Sprites);
            Assert.Collection(sourceInfo.Sprites!,
                sprite =>
                {
                    Assert.Equal("gradient.svg", sprite.Name);
                    Assert.Equal("blue", sprite.Color);
                },
                sprite =>
                {
                    Assert.Equal("gradient2.svg", sprite.Name);
                    Assert.False(sprite.HasColor);
                },
                sprite =>
                {
                    Assert.Equal("light.png", sprite.Name);
                    Assert.True(sprite.HasColor);
                    Assert.Null(sprite.Color);
                });
        }
        finally
        {
            _compilerPool.ReleaseTs(ts);
        }
    }
}
