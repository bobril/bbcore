using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Njsast.EsmToCjs;
using Njsast.Ast;
using Njsast.Output;
using Njsast.Reader;
using Njsast.SourceMap;
using Njsast.Utils;
using Xunit;

namespace Test.EsmToCjs;

public class EsmToCjsTest
{
    [Theory]
    [EsmToCjsDataProvider("Input/EsmToCjs")]
    public void ShouldCorrectlyTransformEsmToCjs(EsmToCjsTestData testData)
    {
        var (outCjs, outCjsMap) = EsmToCjsTestCore(testData);

        AssertEquivalentToTypeScriptOracle(testData, outCjs);
    }

    public static (string outCjs, string outCjsMap) EsmToCjsTestCore(EsmToCjsTestData testData)
    {
        string outCjs;
        string outCjsMap;
        try
        {
            var sourceName = PathUtils.Name(testData.InputFileName);
            var parser = new Parser(
                new Options
                {
                    SourceFile = sourceName,
                    EcmaVersion = 2020,
                    SourceType = SourceType.Module
                },
                testData.InputContent);

            var toplevel = parser.Parse();
            toplevel.FigureOutScope();

            var transformer = new EsmToCjsTreeTransformer();
            transformer.Transform(toplevel);

            toplevel.FigureOutScope();

            var outCjsBuilder = new SourceMapBuilder();
            var outputOptions = new OutputOptions { Beautify = true, Ecma = 6 };
            toplevel.PrintToBuilder(outCjsBuilder, outputOptions);

            outCjs = outCjsBuilder.Content();
            outCjsMap = outCjsBuilder.Build(".", ".").ToString();
        }
        catch (SyntaxError e)
        {
            outCjs = e.Message;
            outCjsMap = "";
        }

        return (outCjs, outCjsMap);
    }

    static void AssertMapEqual(string expected, string actual)
    {
        if (string.IsNullOrEmpty(expected) && string.IsNullOrEmpty(actual))
            return;

        var expectedMap = string.IsNullOrEmpty(expected) ? null : SourceMap.Parse(expected, ".");
        var actualMap = string.IsNullOrEmpty(actual) ? null : SourceMap.Parse(actual, ".");

        if (expectedMap == null && actualMap == null)
            return;
        if (expectedMap == null || actualMap == null)
            throw new Exception($"Source map mismatch: expected {(expectedMap == null ? "null" : "present")}, actual {(actualMap == null ? "null" : "present")}");

        Assert.Equal(expectedMap.version, actualMap.version);
        Assert.Equal(expectedMap.sources.Count, actualMap.sources.Count);
        for (var i = 0; i < expectedMap.sources.Count; i++)
            Assert.Equal(expectedMap.sources[i], actualMap.sources[i]);

        if (expectedMap.names != null || actualMap.names != null)
        {
            Assert.True(expectedMap.names != null && actualMap.names != null, "One source map has names, the other doesn't");
            Assert.Equal(expectedMap.names!.Count, actualMap.names!.Count);
            for (var i = 0; i < expectedMap.names.Count; i++)
                Assert.Equal(expectedMap.names[i], actualMap.names[i]);
        }
    }

    static void AssertEquivalentToTypeScriptOracle(EsmToCjsTestData testData, string actual)
    {
        var actualAst = ParseScript(testData.InputFileName, actual);
        var expectedAst = ParseScript(testData.InputFileName, TypeScriptTranspileToCommonJs(testData));

        Assert.Equal(expectedAst.DumpToString(true), actualAst.DumpToString(true));
    }

    static AstToplevel ParseScript(string fileName, string source)
    {
        var parser = new Parser(
            new Options
            {
                SourceFile = PathUtils.Name(fileName),
                EcmaVersion = 2022,
                SourceType = SourceType.Script
            },
            source);
        return parser.Parse();
    }

    static string TypeScriptTranspileToCommonJs(EsmToCjsTestData testData)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".js");
        File.WriteAllText(tempFile, testData.InputContent);
        try
        {
            var psi = new ProcessStartInfo("node")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            psi.ArgumentList.Add("-e");
            psi.ArgumentList.Add("""
const ts = require('/Users/borisletocha/Research/bbcore/TestProjects/BbApp/node_modules/typescript');
const fs = require('fs');
const fileName = process.argv[1];
const input = fs.readFileSync(fileName, 'utf8');
process.stdout.write(ts.transpileModule(input, {
  compilerOptions: {
    target: ts.ScriptTarget.ES2022,
    module: ts.ModuleKind.CommonJS,
    esModuleInterop: true,
    noEmitHelpers: true,
    allowJs: true
  },
  fileName
}).outputText);
""");
            psi.ArgumentList.Add(tempFile);

            using var process = Process.Start(psi)!;
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new Exception("TypeScript oracle failed: " + error);
            return RemoveObjectDefinePropertyExportsEsModule(output);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    static string RemoveObjectDefinePropertyExportsEsModule(string source)
    {
        return Regex.Replace(source,
            @"\r?\n*Object\.defineProperty\(exports, ""__esModule"",\s*\{\s*value:\s*true\s*\}\);\r?\n",
            "\n");
    }
}
