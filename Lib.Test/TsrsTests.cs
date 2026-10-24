using System;
using System.IO;
using System.Linq;
using Lib.DiskCache;
using Lib.TSCompiler;
using Lib.Utils.Logger;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Lib.Test;

public class TsrsTests
{
    static TSProject LoadProject(string bbrc)
    {
        var fs = new InMemoryFs();
        fs.WriteAllUtf8("/package.json", "{}");
        fs.WriteAllUtf8("/.bbrc", bbrc);
        var dc = new DiskCache.DiskCache(fs, () => fs);
        var project = new TSProject
        {
            DiskCache = dc,
            Owner = (IDirectoryCache)dc.TryGetItem("/")!,
            IsRootProject = true
        };
        project.ProjectOptions = new ProjectOptions
        {
            Owner = project,
            ForbiddenDependencyUpdate = true
        };
        project.LoadProjectJson(true, null);
        return project;
    }

    [Fact]
    public void TsrsEnablesTypecheckAndTranspile()
    {
        var options = LoadProject("{ tsrs: true }").ProjectOptions;

        Assert.True(options.Tsrs);
        Assert.True(options.TsrsTranspile);
        Assert.False(options.ValidateTsrs);
    }

    [Fact]
    public void TsrsTranspileCanBeDisabled()
    {
        var options = LoadProject("{ tsrs: true, tsrsTranspile: false }").ProjectOptions;

        Assert.True(options.Tsrs);
        Assert.False(options.TsrsTranspile);
    }

    [Fact]
    public void TsrsTranspileRequiresTsrs()
    {
        var options = LoadProject("{ tsrsTranspile: true }").ProjectOptions;

        Assert.False(options.Tsrs);
        Assert.False(options.TsrsTranspile);
    }

    [Fact]
    public void ValidateTsrsIsLoadedFromBbrc()
    {
        var options = LoadProject("{ validate: 'tsrs' }").ProjectOptions;

        Assert.True(options.ValidateTsrs);
        Assert.False(options.Validate);
        Assert.False(options.Tsrs);
    }

    [Fact]
    public void TsrsPlatformBinaryIsFoundInParentNodeModules()
    {
        var fs = new InMemoryFs();
        var package = TsrsLocator.PlatformPackageNames().First();
        var executable = OperatingSystem.IsWindows() ? "tsrs.exe" : "tsrs";
        fs.WriteAllUtf8("/workspace/node_modules/@tsrs/" + package + "/" + executable, "");

        Assert.True(TsrsLocator.TryFindInNodeModules("/workspace/node_modules", fs, out var tsrsExecutable));
        Assert.Equal("/workspace/node_modules/@tsrs/" + package + "/" + executable, tsrsExecutable);
        Assert.False(TsrsLocator.TryFindInNodeModules("/other/node_modules", fs, out _));
    }

    [Fact]
    public void NativeTypeCheckerUsesSeparateOverlayForTsrs()
    {
        Assert.Equal(".bbcore-tsgo-typecheck.json",
            NativeTsCompiler.GetTypeCheckProjectFileName(NativeTypeScriptKind.Tsgo));
        Assert.Equal(".bbcore-tsrs-typecheck.json",
            NativeTsCompiler.GetTypeCheckProjectFileName(NativeTypeScriptKind.Tsrs));
    }

    [Fact]
    public void CompilerOptionsUseTypeScript7EnumNumbers()
    {
        var options = TsrsTranspiler.ToApiCompilerOptions(new TSCompilerOptions
        {
            target = ScriptTarget.Es2022,
            module = ModuleKind.Esnext,
            jsx = JsxEmit.React,
            newLine = NewLineKind.LineFeed,
            moduleResolution = ModuleResolutionKind.Bundler,
            baseUrl = ".",
            lib = new System.Collections.Generic.HashSet<string> { "es2022", "dom" },
            reactNamespace = "b",
            experimentalDecorators = true
        });

        Assert.Equal(9, options["target"]!.Value<int>());
        Assert.Equal(99, options["module"]!.Value<int>());
        Assert.Equal(2, options["jsx"]!.Value<int>());
        Assert.Equal(2, options["newLine"]!.Value<int>());
        Assert.Equal(100, options["moduleResolution"]!.Value<int>());
        Assert.Null(options["baseUrl"]);
        Assert.Null(options["lib"]);
        Assert.Equal("b", options["reactNamespace"]!.Value<string>());
        Assert.True(options["experimentalDecorators"]!.Value<bool>());
    }

    [Fact]
    public void CompilerOptionsMapCommonJsAndCrLf()
    {
        var options = TsrsTranspiler.ToApiCompilerOptions(new TSCompilerOptions
        {
            module = ModuleKind.Commonjs,
            newLine = NewLineKind.CarriageReturnLineFeed
        });

        Assert.Equal(1, options["module"]!.Value<int>());
        Assert.Equal(1, options["newLine"]!.Value<int>());
        Assert.Null(options["target"]);
    }

    [Fact]
    public void TranspileResponseIsParsed()
    {
        var response = JObject.Parse("""
            {"outputText":"\"use strict\";\nlet = ;\n//# sourceMappingURL=a.js.map","sourceMapText":"{\"version\":3}",
             "diagnostics":[
               {"pos":59,"end":60,"fileName":"/src/a.tsx","startPosition":{"line":2,"character":6},
                "endPosition":{"line":2,"character":7},"code":1109,"category":1,"text":"Expression expected.",
                "messageChain":[{"text":"First","messageChain":[{"text":"Nested"}]}]},
               {"pos":-1,"end":-1,"code":5102,"category":0,"text":"Option 'baseUrl' has been removed."}
             ]}
            """);

        var result = TsrsTranspiler.ParseTranspileResponse("src/a.tsx", response);

        Assert.StartsWith("\"use strict\";", result.JavaScript);
        Assert.Equal("{\"version\":3}", result.SourceMap);
        Assert.Equal(2, result.Diagnostics!.Count);
        var fileDiagnostic = result.Diagnostics[0];
        Assert.True(fileDiagnostic.IsError);
        Assert.Equal(1109, fileDiagnostic.Code);
        Assert.Equal("src/a.tsx", fileDiagnostic.FileName);
        Assert.Equal(2, fileDiagnostic.StartLine);
        Assert.Equal(6, fileDiagnostic.StartCol);
        Assert.Equal(2, fileDiagnostic.EndLine);
        Assert.Equal(7, fileDiagnostic.EndCol);
        Assert.Equal("Expression expected.\n  First\n    Nested", fileDiagnostic.Text);
        var globalDiagnostic = result.Diagnostics[1];
        Assert.False(globalDiagnostic.IsError);
        Assert.Equal(5102, globalDiagnostic.Code);
        Assert.Null(globalDiagnostic.FileName);
    }

    [Fact]
    public void PatcherUnwrapsIndirectCalls()
    {
        var (js, _) = TsrsOutputPatcher.Patch(
            "const g11n_1 = require(\"bobril-g11n\");\nlet a = (0, g11n_1.t)(\"Hello\", (0, g11n_1.f)(1));\n" +
            "let b = (0, x)(substring(0, 1));\n", null);

        Assert.Equal(
            "const g11n_1 = require(\"bobril-g11n\");\nlet a = g11n_1.t(\"Hello\", g11n_1.f(1));\n" +
            "let b = x(substring(0, 1));\n", js);
    }

    [Fact]
    public void PatcherKeepsIndirectCallsWrittenInSource()
    {
        const string source = "import { t } from \"g\";\nt(\"a\");\n(0, eval)(\"1\");\n(0,vn.codeFrame)(x);\n";

        var (js, _) = TsrsOutputPatcher.Patch(
            "const g_1 = require(\"g\");\n(0, g_1.t)(\"a\");\n(0, eval)(\"1\");\n(0, vn.codeFrame)(x);\n", null, source);

        Assert.Equal("const g_1 = require(\"g\");\ng_1.t(\"a\");\n(0, eval)(\"1\");\n(0, vn.codeFrame)(x);\n", js);
    }

    [Fact]
    public void PatcherRemovesEsModuleTagging()
    {
        var (js, _) = TsrsOutputPatcher.Patch(
            "\"use strict\";\nObject.defineProperty(exports, \"__esModule\", { value: true });\nexports.a = 1;\n", null);

        Assert.Equal("\"use strict\";\nexports.a = 1;\n", js);
    }

    [Fact]
    public void PatcherAddsSettersToReexports()
    {
        var (js, _) = TsrsOutputPatcher.Patch("""
            var flux_1 = require("./flux");
            Object.defineProperty(exports, "createAction", { enumerable: true, get: function () { return flux_1.createAction; } });
            const app_1 = require("./app");
            Object.defineProperty(exports, "app", { enumerable: true, get: function () { return app_1.app; } });
            Object.defineProperty(exports, "app2", { enumerable: true, get: function () { return app_1.app; } });

            """, null);

        Assert.Equal("""
            var flux_1 = require("./flux");
            Object.defineProperty(exports, "createAction", { enumerable: true, get: function () { return flux_1.createAction; }, set: function (v) { flux_1.createAction = v; } });
            const app_1 = require("./app");
            Object.defineProperty(exports, "app", { enumerable: true, get: function () { return app_1.app; }, set: function (v) { exports.app = exports.app2 = app_1.app = v; } });
            Object.defineProperty(exports, "app2", { enumerable: true, get: function () { return app_1.app; }, set: function (v) { exports.app = exports.app2 = app_1.app = v; } });

            """, js);
    }

    [Fact]
    public void PatcherSplitsLongExportedNamesInitialization()
    {
        var names = Enumerable.Range(0, 60).Select(i => "n" + i).ToList();
        var input = string.Concat(Enumerable.Reverse(names).Select(n => "exports." + n + " = ")) + "void 0;\n";

        var (js, _) = TsrsOutputPatcher.Patch(input, null);

        Assert.Equal(
            string.Concat(Enumerable.Reverse(names.Take(50)).Select(n => "exports." + n + " = ")) + "void 0;\n" +
            string.Concat(Enumerable.Reverse(names.Skip(50)).Select(n => "exports." + n + " = ")) + "void 0;\n", js);
        var shortInput = string.Concat(Enumerable.Reverse(names.Take(50)).Select(n => "exports." + n + " = ")) +
                         "void 0;\n";
        Assert.Equal(shortInput, TsrsOutputPatcher.Patch(shortInput, null).JavaScript);
    }

    [Fact]
    public void PatcherEmitsDestructuredExportsLikeTypeScript6()
    {
        // tsrs output; expected is TypeScript 6 output with bbcore patches
        var (js, _) = TsrsOutputPatcher.Patch("""
            "use strict";
            Object.defineProperty(exports, "__esModule", { value: true });
            exports.k2 = exports.k1 = exports.q = exports.p = exports.b = exports.a = exports.getA = exports.only = void 0;
            const r_1 = require("./r");
            let _a = 1;
            [exports.only] = (0, r_1.r)();
            ({ get: exports.getA } = (0, r_1.r)());
            ({ a: exports.a, b: exports.b } = (0, r_1.r)() ? x : y);
            [exports.p, , exports.q] = (0, r_1.r)();
            ({ "k-1": exports.k1, k2: exports.k2 } = (0, r_1.r)());
            ({ c: exports.c = 1 } = (0, r_1.r)());

            """, null);

        Assert.Equal("""
            "use strict";
            var _b, _c, _d;
            exports.k2 = exports.k1 = exports.q = exports.p = exports.b = exports.a = exports.getA = exports.only = void 0;
            const r_1 = require("./r");
            let _a = 1;
            exports.only = r_1.r()[0];
            exports.getA = r_1.r().get;
            _b = r_1.r() ? x : y, exports.a = _b.a, exports.b = _b.b;
            _c = r_1.r(), exports.p = _c[0], exports.q = _c[2];
            _d = r_1.r(), exports.k1 = _d["k-1"], exports.k2 = _d.k2;
            ({ c: exports.c = 1 } = r_1.r());

            """, js);
    }

    [Fact]
    public void PatcherKeepsSourceMapInSync()
    {
        // tsrs output of a.ts:
        // import { t } from "m";
        // export { x } from "y";
        //
        // let a = t("x", t("y"));
        const string input = """
            "use strict";
            Object.defineProperty(exports, "__esModule", { value: true });
            exports.x = void 0;
            const m_1 = require("m");
            var y_1 = require("y");
            Object.defineProperty(exports, "x", { enumerable: true, get: function () { return y_1.x; } });
            let a = (0, m_1.t)("x", (0, m_1.t)("y"));
            //# sourceMappingURL=a.js.map
            """;
        const string map = """
            {"version":3,"file":"a.js","sourceRoot":"","sources":["a.ts"],"names":[],"mappings":";;;AAAA,yBAAsB;AACtB,uBAAsB;AAAb,sFAAA,CAAC,OAAA;AAEV,IAAI,CAAC,GAAG,IAAA,KAAC,EAAC,GAAG,EAAE,IAAA,KAAC,EAAC,GAAG,CAAC,CAAC,CAAC"}
            """;

        var (js, patchedMap) = TsrsOutputPatcher.Patch(input, map);

        Assert.Equal("""
            "use strict";
            exports.x = void 0;
            const m_1 = require("m");
            var y_1 = require("y");
            Object.defineProperty(exports, "x", { enumerable: true, get: function () { return y_1.x; }, set: function (v) { y_1.x = v; } });
            let a = m_1.t("x", m_1.t("y"));
            //# sourceMappingURL=a.js.map

            """, js);
        var parsed = Njsast.SourceMap.SourceMap.Parse(patchedMap!, null);
        // FindPosition uses 1-based lines and columns
        // `const m_1 = require("m");` comes from line 1
        Assert.Equal(1, parsed.FindPosition(3, 1).Line);
        // `let a` from line 4, `"x"` argument (output column 15) from line 4 column 11
        Assert.Equal(4, parsed.FindPosition(6, 1).Line);
        var x = parsed.FindPosition(6, 15);
        Assert.Equal(4, x.Line);
        Assert.Equal(11, x.Col);
        // `"y"` (output column 26) from line 4 column 18
        var y = parsed.FindPosition(6, 26);
        Assert.Equal(4, y.Line);
        Assert.Equal(18, y.Col);
    }

    [Fact]
    public void RealTsrsTranspilesWhenAvailable()
    {
        // Runs only when TSRS_BINARY points to a tsrs native binary
        var tsrsExecutable = Environment.GetEnvironmentVariable(TsrsLocator.BinaryEnvironmentVariable);
        if (string.IsNullOrEmpty(tsrsExecutable) || !File.Exists(tsrsExecutable))
            return;

        using var transpiler = new TsrsTranspiler(tsrsExecutable, new DummyLogger());
        transpiler.CompilerOptions = new TSCompilerOptions
        {
            module = ModuleKind.Commonjs,
            target = ScriptTarget.Es2022,
            jsx = JsxEmit.React,
            reactNamespace = "b",
            sourceMap = true
        };

        var result = transpiler.Transpile("src/index.tsx", "export const a: number = 1;\nexport const e = <div/>;");
        Assert.Null(result.Diagnostics);
        Assert.Contains("exports.a = 1;", result.JavaScript);
        Assert.Contains("b.createElement(\"div\", null)", result.JavaScript);
        Assert.NotNull(result.SourceMap);

        var broken = transpiler.Transpile("src/broken.ts", "let = ;");
        Assert.Contains(broken.Diagnostics!, d => d.IsError && d.Code == 1109 && d.FileName == "src/broken.ts");
        Assert.StartsWith("7.", transpiler.GetTSVersion());
    }
}
