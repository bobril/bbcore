using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Njsast.Ast;
using Njsast.AstDump;
using Njsast.Jsx;
using Njsast.Output;
using Njsast.Reader;
using Njsast.SourceMap;
using Njsast.Utils;
using Xunit;

namespace Test.Jsx;

public class JsxToCreateElementTest
{
    public static IEnumerable<object[]> BabelParserJsxFixtures()
    {
        return Directory
            .EnumerateFiles("Input/Jsx/BabelParser", "input.js", SearchOption.AllDirectories)
            .Select(PathUtils.Normalize)
            .Where(path => !path.Contains("/errors/"))
            // Babel accepts this as script-mode JSX fixture; TypeScript's JSX transform rejects the
            // leading HTML comment, so it cannot participate in the TypeScript oracle comparison.
            .Where(path => path != "Input/Jsx/BabelParser/basic/html-comment/input.js")
            .Select(path => new object[] { path, File.ReadAllText(path) });
    }

    [Fact]
    public void LowersIntrinsicTagWithBobrilFactory()
    {
        AssertLowered(
            "const view=<div id=\"root\" disabled>{title}</div>;",
            "const view=b.createElement(\"div\",{id:\"root\",disabled:true},title)",
            BobrilOptions());
    }

    [Fact]
    public void LowersMemberTagAndObjectSpreadProps()
    {
        AssertLowered(
            "const view=<Foo.Bar a=\"1\" {...props} b={n} />;",
            "const view=b.createElement(Foo.Bar,{a:\"1\",...props,b:n})",
            BobrilOptions());
    }

    [Fact]
    public void LowersLowercaseMemberTagAsExpression()
    {
        AssertLowered(
            "const view=<b.Component />;",
            "const view=b.createElement(b.Component,null)",
            BobrilOptions());
    }

    [Fact]
    public void AllowsWhitespaceAfterJsxMemberDot()
    {
        AssertLowered(
            "const view=<obj. MemberClassComponent />;",
            "const view=b.createElement(obj.MemberClassComponent,null)",
            BobrilOptions());
    }

    [Fact]
    public void LowersFragmentWithConfiguredFragmentSymbol()
    {
        AssertLowered(
            "const view=<>text<span />{...items}</>;",
            "const view=b.createElement(b.Fragment,null,\"text\",b.createElement(\"span\",null),...items)",
            BobrilOptions());
    }

    [Fact]
    public void IgnoresIndentationOnlyText()
    {
        AssertLowered(
            "const view=<div>\n  <span />\n</div>;",
            "const view=b.createElement(\"div\",null,b.createElement(\"span\",null))",
            BobrilOptions());
    }

    [Fact]
    public void PreservesTextImmediatelyAfterExpressionChild()
    {
        AssertLowered(
            "const view=<b>{p}\u200b\u200b</b>;",
            "const view=b.createElement(\"b\",null,p,\"\u200b\u200b\")",
            BobrilOptions());
    }

    [Fact]
    public void PreservesParenthesizedTextAroundExpressionChild()
    {
        AssertLowered(
            "const view=<Label>({text})</Label>;",
            "const view=b.createElement(Label,null,\"(\",text,\")\")",
            BobrilOptions());
    }

    [Fact]
    public void UsesReactDefaults()
    {
        AssertLowered(
            "const view=<Component attr={<b />} />;",
            "const view=React.createElement(Component,{attr:React.createElement(\"b\",null)})",
            new JsxToCreateElementOptions());
    }

    [Fact]
    public void UsesParserOptionsFactory()
    {
        var toplevel = new Parser(new Options { EcmaVersion = 2022, ParseJSX = true }, "const view=<><span /></>;").Parse();
        var options = new Options
        {
            JsxFactory = "b.createElement",
            JsxFragmentFactory = "b.Fragment"
        };

        var transformed = (AstToplevel)new JsxToCreateElementTreeTransformer(options).Transform(toplevel);

        Assert.Equal("const view=b.createElement(b.Fragment,null,b.createElement(\"span\",null))",
            transformed.PrintToString(new OutputOptions { Ecma = 2022 }));
    }

    [Theory]
    [MemberData(nameof(BabelParserJsxFixtures))]
    public void BabelParserJsxFixtureShouldMatchTypeScriptReactOracle(string fixturePath, string source)
    {
        var options = new Options
        {
            SourceFile = fixturePath,
            SourceType = SourceType.Module,
            EcmaVersion = 2022,
            ParseJSX = true
        };
        var toplevel = new Parser(options, source).Parse();
        var transformed = (AstToplevel)new JsxToCreateElementTreeTransformer(options).Transform(toplevel);
        var actualJs = transformed.PrintToString(new OutputOptions { Ecma = 2022, Beautify = true });
        var expectedJs = TranspileJsxWithTypeScript60(source, fixturePath).Replace("\"use strict\";\n", "");

        Assert.Equal(
            DumpJavaScriptAstWithoutPositions(expectedJs),
            DumpJavaScriptAstWithoutPositions(actualJs));
    }

    static JsxToCreateElementOptions BobrilOptions()
    {
        return new()
        {
            Factory = "b.createElement",
            Fragment = "b.Fragment"
        };
    }

    static void AssertLowered(string source, string expected, JsxToCreateElementOptions options)
    {
        var toplevel = new Parser(new Options { EcmaVersion = 2022, ParseJSX = true }, source).Parse();
        var transformed = (AstToplevel)new JsxToCreateElementTreeTransformer(options).Transform(toplevel);
        Assert.Equal(expected, transformed.PrintToString(new OutputOptions { Ecma = 2022 }));
    }

    static string TranspileJsxWithTypeScript60(string input, string fixturePath)
    {
        var encoded = System.Convert.ToBase64String(Encoding.UTF8.GetBytes(input));
        var script = """
            const ts = require('./TestProjects/BbApp/node_modules/typescript');
            if (!ts.version.startsWith('6.0.')) {
              throw new Error(`Expected TypeScript 6.0, got ${ts.version}`);
            }
            const input = Buffer.from(process.argv[1], 'base64').toString('utf8');
            const output = ts.transpileModule(input, {
              compilerOptions: {
                target: ts.ScriptTarget.ES2022,
                module: ts.ModuleKind.ESNext,
                jsx: ts.JsxEmit.React,
                jsxFactory: 'React.createElement',
                jsxFragmentFactory: 'React.Fragment',
                useDefineForClassFields: false
              },
              fileName: process.argv[2],
              reportDiagnostics: true
            });
            const errors = (output.diagnostics || [])
              .filter(d => d.category === ts.DiagnosticCategory.Error)
              .map(d => ts.flattenDiagnosticMessageText(d.messageText, '\n'));
            if (errors.length) {
              console.error(errors.join('\n'));
              process.exit(1);
            }
            process.stdout.write(output.outputText);
            """;

        var startInfo = new ProcessStartInfo("node")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = FindBbcoreRoot()
        };
        startInfo.ArgumentList.Add("-e");
        startInfo.ArgumentList.Add(script);
        startInfo.ArgumentList.Add(encoded);
        startInfo.ArgumentList.Add(Path.ChangeExtension(fixturePath, ".tsx"));

        using var process = Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
        Assert.Empty(error);
        return output;
    }

    static string DumpJavaScriptAstWithoutPositions(string input)
    {
        var toplevel = Parser.Parse(input, new Options
        {
            SourceFile = "oracle.js",
            SourceType = SourceType.Module,
            EcmaVersion = 2022
        });
        var sink = new StringLineSink();
        var dumper = new DumpAst(new AstDumpWriter(sink, withoutPositions: true));
        dumper.Walk(toplevel);
        return sink.ToString();
    }

    static string FindBbcoreRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "bbcore.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not find repository root containing bbcore.sln");
    }
}
