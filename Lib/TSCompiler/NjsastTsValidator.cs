using System;
using System.IO;
using System.Threading.Tasks;
using Njsast;
using Njsast.Ast;
using Njsast.EsmToCjs;
using Njsast.Jsx;
using Njsast.Output;
using Njsast.Reader;

namespace Lib.TSCompiler;

public static class NjsastTsValidator
{
    const string BuildinMode = "Buildin";
    const string ValidateTsMode = "ValidateTS";
    public const string BuildinTranspilerCacheKey = "NjsastBuildinTranspiler-v1";

    public static bool BuildinEnabled =>
        string.Equals(Environment.GetEnvironmentVariable("BBMODE"), BuildinMode, StringComparison.Ordinal);

    public static bool IsBuildinEnabled(bool future, bool validate) => !validate && (future || BuildinEnabled);

    public static bool Enabled =>
        string.Equals(Environment.GetEnvironmentVariable("BBMODE"), ValidateTsMode, StringComparison.Ordinal);

    public static bool IsEnabled(bool validate) => validate || Enabled;

    public static Task<string>? StartTranspile(string fileName, string source)
    {
        if (!IsTypeScriptLike(fileName)) return null;
        return Task.Run(() => TranspileToCommonJs(fileName, source));
    }

    public static void Validate(string projectDir, string fileName, string source, string v8JavaScript,
        Task<string>? njsastTask, Action<string> log)
    {
        if (njsastTask == null) return;
        string njsastJavaScript;
        try
        {
            njsastJavaScript = njsastTask.GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            log("Njsast ValidateTS failed for " + fileName + ": " + ex.Message);
            var failedTestCase = WriteRegressionTestCase(projectDir, fileName, source, v8JavaScript,
                "// Njsast failed: " + ex + "\n");
            log("Njsast ValidateTS wrote repro " + failedTestCase);
            return;
        }

        string formattedV8;
        string formattedNjsast;
        try
        {
            formattedV8 = FormatJavaScript(v8JavaScript, removeEsModuleTagging: true);
            formattedNjsast = FormatJavaScript(njsastJavaScript, removeEsModuleTagging: true);
        }
        catch (Exception ex)
        {
            log("Njsast ValidateTS normalization failed for " + fileName + ": " + ex.Message);
            var failedTestCase = WriteRegressionTestCase(projectDir, fileName, source, v8JavaScript,
                "// Njsast normalization failed: " + ex + "\n\n" + njsastJavaScript);
            log("Njsast ValidateTS wrote repro " + failedTestCase);
            return;
        }

        if (formattedV8 == formattedNjsast) return;

        var testCase = WriteRegressionTestCase(projectDir, fileName, source, formattedV8,
            formattedNjsast);
        log("Njsast ValidateTS output differs for " + fileName + "; wrote " + testCase);
    }

    public static string TranspileToCommonJs(string fileName, string source)
    {
        return TranspileToCommonJsAst(fileName, source).JavaScript;
    }

    public static (string JavaScript, AstToplevel Ast) TranspileToCommonJsAst(string fileName, string source)
    {
        var options = new Options
        {
            SourceType = SourceType.Module,
            JsxFactory = "b.createElement",
            JsxFragmentFactory = "b.Fragment",
            PreserveConstEnums = true
        };
        var ast = IsTypeScriptLike(fileName)
            ? IsTsxLike(fileName)
                ? TypeScriptParser.ParseTsx(source, options)
                : TypeScriptParser.Parse(source, options)
            : Parser.Parse(source, new Options
            {
                SourceType = SourceType.Module,
                EcmaVersion = 2022,
                ParseJSX = IsJsxLike(fileName)
            });

        if (IsTsxLike(fileName) || IsJsxLike(fileName))
        {
            ast = (AstToplevel)new JsxToCreateElementTreeTransformer(options).Transform(ast);
        }

        ast.FigureOutScope();
        ast = (AstToplevel)new EsmToCjsTreeTransformer(includeExportSetters: true).Transform(ast);
        ast.FigureOutScope();
        return (ast.PrintToString(new OutputOptions { Beautify = true, UpdateNodePositions = true }), ast);
    }

    public static string FormatJavaScript(string javascript, bool removeEsModuleTagging = false)
    {
        var withoutSourceMap = Njsast.SourceMap.SourceMap.RemoveLinkToSourceMap(javascript);
        var ast = new Parser(new Options { SourceType = SourceType.Module }, withoutSourceMap).Parse();
        if (removeEsModuleTagging)
            ast = (AstToplevel)new EsModuleTaggingEraseTransformer().Transform(ast);
        return ast.PrintToString(new OutputOptions { Beautify = true });
    }

    sealed class EsModuleTaggingEraseTransformer : TreeTransformer
    {
        protected override AstNode? Before(AstNode node, bool inList)
        {
            return IsEsModuleTaggingStatement(node) ? Remove : null;
        }

        protected override AstNode? After(AstNode node, bool inList)
        {
            return null;
        }

        static bool IsEsModuleTaggingStatement(AstNode node)
        {
            return node is AstSimpleStatement
            {
                Body: AstCall
                {
                    Expression: AstDot
                    {
                        Expression: AstSymbolRef { Name: "Object" },
                        Property: "defineProperty"
                    },
                    Args.Count: 3
                } call
            } && call.Args[0] is AstSymbolRef { Name: "exports" }
              && call.Args[1] is AstString { Value: "__esModule" }
              && call.Args[2] is AstObject objectArg
              && IsValueTrueObject(objectArg);
        }

        static bool IsValueTrueObject(AstObject objectArg)
        {
            if (objectArg.Properties.Count != 1) return false;
            return objectArg.Properties[0] is AstObjectKeyVal
            {
                Key: AstSymbol { Name: "value" } or AstString { Value: "value" },
                Value: AstTrue
            };
        }
    }

    public static string WriteRegressionTestCase(string projectDir, string fileName, string source,
        string v8JavaScript, string njsastJavaScript)
    {
        projectDir = Path.GetFullPath(projectDir);
        var inputDir = Path.Combine(projectDir, ".bbcore", "Input");
        var wrongDir = Path.Combine(projectDir, ".bbcore", "Wrong");
        Directory.CreateDirectory(inputDir);
        Directory.CreateDirectory(wrongDir);
        var baseName = Path.GetFileName(fileName);
        if (string.IsNullOrEmpty(baseName))
            baseName = "input.ts";

        var sourcePath = Path.GetFullPath(Path.Combine(inputDir, baseName));
        var expectedName = baseName + ".expected.js";
        File.WriteAllText(sourcePath, source);
        File.WriteAllText(Path.Combine(inputDir, expectedName), v8JavaScript);
        File.WriteAllText(Path.Combine(wrongDir, expectedName), njsastJavaScript);
        return sourcePath;
    }

    static bool IsTypeScriptLike(string fileName)
    {
        return fileName.EndsWith(".ts", StringComparison.Ordinal) ||
               fileName.EndsWith(".tsx", StringComparison.Ordinal) ||
               fileName.EndsWith(".mts", StringComparison.Ordinal) ||
               fileName.EndsWith(".mtsx", StringComparison.Ordinal);
    }

    static bool IsTsxLike(string fileName)
    {
        return fileName.EndsWith(".tsx", StringComparison.Ordinal) ||
               fileName.EndsWith(".mtsx", StringComparison.Ordinal);
    }

    static bool IsJsxLike(string fileName)
    {
        return fileName.EndsWith(".jsx", StringComparison.Ordinal) ||
               fileName.EndsWith(".mjsx", StringComparison.Ordinal);
    }
}
