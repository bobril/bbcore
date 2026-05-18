using System;
using System.Collections.Generic;
using Njsast.Ast;
using Njsast.AstDump;
using Njsast.Bobril;
using Njsast.Output;
using Njsast.Reader;
using Njsast.SourceMap;
using Njsast.Utils;
using Xunit;

namespace Test.Reader;

public class ParserTest
{
    const string SimpleVarStatement = "var a = 1";

    const string SimpleVarStatementAst =
        "Toplevel 1:1 - 1:10\n  Var 1:1 - 1:10\n    VarDef 1:5 - 1:10\n      SymbolVar 1:5 - 1:6\n        Name: a\n      Number 1:9 - 1:10\n        Value: 1\n        Literal: 1\n";

    const string SimpleVarStatementMultilineAst =
        "Toplevel 1:1 - 4:2\n  Var 1:1 - 4:2\n    VarDef 2:1 - 4:2\n      SymbolVar 2:1 - 2:2\n        Name: a\n      Number 4:1 - 4:2\n        Value: 1\n        Literal: 1\n";

    [Theory]
    [ParserTestDataProvider("Input/Parser")]
    public void ParserShouldProduceExpectedResultOrSyntaxError(ParserTestData testData)
    {
        var (outAst, outMinJs, outMinJsMap, outNiceJs, outNiceJsMap) = ParseTestCore(testData);

        Assert.Equal(testData.ExpectedAst, outAst);
        Assert.Equal(testData.ExpectedMinJs, outMinJs);
        Assert.Equal(testData.ExpectedMinJsMap, outMinJsMap);
        Assert.Equal(testData.ExpectedNiceJs, outNiceJs);
        Assert.Equal(testData.ExpectedNiceJsMap, outNiceJsMap);
    }

    public static (string outAst, string outMinJs, string outMinJsMap, string outNiceJs, string outNiceJsMap)
        ParseTestCore(
            ParserTestData testData)
    {
        string outAst;
        var outMinJs = string.Empty;
        var outMinJsMap = string.Empty;
        var outNiceJs = string.Empty;
        var outNiceJsMap = string.Empty;
        try
        {
            var comments = new List<(bool block, string content, SourceLocation location)>();
            var commentListener = new CommentListener();
            var parser = new Parser(
                new()
                {
                    SourceFile = testData.SourceName, EcmaVersion = testData.EcmaScriptVersion, OnComment =
                        (block, content, location) =>
                        {
                            commentListener.OnComment(block, content, location);
                            comments.Add((block, content, location));
                        },
                    SourceType = testData.SourceName.StartsWith("module-") ? SourceType.Module : SourceType.Script,
                    ParseJSX = testData.SourceName.Contains("jsx", StringComparison.OrdinalIgnoreCase)
                },
                testData.Input);
            var toplevel = parser.Parse();
            commentListener.Walk(toplevel);
            SourceMap? inputSourceMap = null;
            if (testData.InputSourceMap != null)
            {
                inputSourceMap = SourceMap.Parse(testData.InputSourceMap, ".");
                inputSourceMap.ResolveInAst(toplevel);
            }

            var strSink = new StringLineSink();
            toplevel.FigureOutScope();
            var dumper = new DumpAst(new AstDumpWriter(strSink));
            dumper.Walk(toplevel);
            foreach (var (block, content, location) in comments)
            {
                strSink.Print(
                    $"{(block ? "Block" : "Line")} Comment ({location.Start.ToShortString()}-{location.End.ToShortString()}): {content}");
            }

            outAst = strSink.ToString();
            var outMinJsBuilder = new SourceMapBuilder();
            var outputOptions = new OutputOptions();
            toplevel.PrintToBuilder(outMinJsBuilder, outputOptions);
            outMinJsBuilder.AddText(
                $"//# sourceMappingURL={PathUtils.ChangeExtension(testData.SourceName, "minjs.map")}");
            if (inputSourceMap!=null) outMinJsBuilder.AttachSourcesContent(inputSourceMap);
            outMinJs = outMinJsBuilder.Content();
            outMinJsMap = outMinJsBuilder.Build(".", ".").ToString();
            var outNiceJsBuilder = new SourceMapBuilder();
            outputOptions = new()
            {
                Beautify = true
            };
            toplevel.PrintToBuilder(outNiceJsBuilder, outputOptions);
            outNiceJsBuilder.AddText(
                $"//# sourceMappingURL={PathUtils.ChangeExtension(testData.SourceName, "nicejs.map")}");
            if (inputSourceMap!=null) outNiceJsBuilder.AttachSourcesContent(inputSourceMap);
            outNiceJs = outNiceJsBuilder.Content();
            outNiceJsMap = outNiceJsBuilder.Build(".", ".").ToString();

            strSink = new StringLineSink();
            toplevel.FigureOutScope();
            dumper = new DumpAst(new AstDumpWriter(strSink));
            dumper.Walk(toplevel);
            var beforeClone = strSink.ToString();
            toplevel = toplevel.DeepClone();
            strSink = new StringLineSink();
            toplevel.FigureOutScope();
            dumper = new DumpAst(new AstDumpWriter(strSink));
            dumper.Walk(toplevel);
            var afterClone = strSink.ToString();
            if (beforeClone != afterClone)
            {
                throw new Exception("Dump of clone is not identical");
            }

            toplevel.Mangle();
        }
        catch (SyntaxError e)
        {
            outAst = e.Message;
        }

        return (outAst, outMinJs, outMinJsMap, outNiceJs, outNiceJsMap);
    }

    [Theory]
    // More info about white space characters https://en.wikipedia.org/wiki/Whitespace_character
    [InlineData('\u0009' /*CHARACTER TABULATION*/, SimpleVarStatementAst)]
    [InlineData('\u000b' /*LINE TABULATION*/, SimpleVarStatementAst)]
    [InlineData('\u000c' /*FORM FEED*/, SimpleVarStatementAst)]
    [InlineData('\u0020' /*SPACE*/, SimpleVarStatementAst)]
    [InlineData('\u00a0' /*NO-BREAK SPACE*/, SimpleVarStatementAst)]
    [InlineData('\ufeff' /*ZERO WIDTH NO-BREAK SPACE*/, SimpleVarStatementAst)]
    [InlineData('\u1680' /*OGHAM SPACE MARK*/, SimpleVarStatementAst)]
    [InlineData('\u2000' /*EN QUAD*/, SimpleVarStatementAst)]
    [InlineData('\u2001' /*EM QUAD*/, SimpleVarStatementAst)]
    [InlineData('\u2002' /*EN SPACE*/, SimpleVarStatementAst)]
    [InlineData('\u2003' /*EM SPACE*/, SimpleVarStatementAst)]
    [InlineData('\u2004' /*THREE-PER-EM SPACE*/, SimpleVarStatementAst)]
    [InlineData('\u2005' /*FOUR-PER-EM SPACE*/, SimpleVarStatementAst)]
    [InlineData('\u2006' /*SIX-PER-EM SPACE*/, SimpleVarStatementAst)]
    [InlineData('\u2007' /*FIGURE SPACE*/, SimpleVarStatementAst)]
    [InlineData('\u2008' /*PUNCTUATION SPACE*/, SimpleVarStatementAst)]
    [InlineData('\u2009' /*THIN SPACE*/, SimpleVarStatementAst)]
    [InlineData('\u200A' /*HAIR SPACE*/, SimpleVarStatementAst)]
    [InlineData('\u202F' /*NARROW NO-BREAK SPACE*/, SimpleVarStatementAst)]
    [InlineData('\u205F' /*MEDIUM MATHEMATICAL SPACE*/, SimpleVarStatementAst)]
    [InlineData('\u3000' /*IDEOGRAPHIC SPACE*/, SimpleVarStatementAst)]
    [InlineData('\u180e' /*MONGOLIAN VOWEL SEPARATOR*/, "Unexpected character '\u180e' (1:4)")]
    [InlineData('\u200b' /*ZERO WIDTH SPACE*/, "Unexpected character '\u200b' (1:4)")]
    [InlineData('\u200c' /*ZERO WIDTH NON-JOINER*/, "Unexpected character '\u200c' (1:8)")]
    [InlineData('\u200d' /*ZERO WIDTH JOINER*/, "Unexpected character '\u200d' (1:8)")]
    [InlineData('\u2060' /*WORD JOINER*/, "Unexpected character '\u2060' (1:4)")]
    [InlineData('\u0085' /*NEXT LINE*/, SimpleVarStatementAst)]
    [InlineData('\u000a' /*LINE FEED*/, SimpleVarStatementMultilineAst)]
    [InlineData('\u000d' /*CARRIAGE RETURN*/, SimpleVarStatementMultilineAst)]
    [InlineData('\u2028' /*LINE SEPARATOR*/, SimpleVarStatementMultilineAst)]
    [InlineData('\u2029' /*PARAGRAPH SEPARATOR*/, SimpleVarStatementMultilineAst)]
    public void ParserShouldSkipAllowedUnicodeWhiteSpaceCharactersOrProduceSyntaxError(char whiteSpaceChar,
        string expectedAst)
    {
        var input = SimpleVarStatement.Replace(' ', whiteSpaceChar);
        string outAst;
        try
        {
            var parser = new Parser(new Options(), input);
            var toplevel = parser.Parse();
            var strSink = new StringLineSink();
            var dumper = new DumpAst(new AstDumpWriter(strSink));
            dumper.Walk(toplevel);
            outAst = strSink.ToString();
        }
        catch (SyntaxError e)
        {
            outAst = e.Message;
        }

        Assert.Equal(expectedAst, outAst);
    }

    [Fact]
    public void ParserShouldAcceptInvalidTemplateEscapeInTaggedTemplate()
    {
        var parser = new Parser(new Options(), @"tag`\unicode`;");

        var exception = Record.Exception(() => parser.Parse());

        Assert.Null(exception);
    }

    [Fact]
    public void ParserShouldRejectInvalidTemplateEscapeInUntaggedTemplate()
    {
        var parser = new Parser(new Options(), @"`\unicode`;");

        var exception = Assert.Throws<SyntaxError>(() => parser.Parse());

        Assert.Equal("Bad escape sequence in untagged template literal (1:2)", exception.Message);
    }

    [Fact]
    public void ParserShouldSkipHashbangWhenEnabled()
    {
        var parser = new Parser(new Options { AllowHashBang = true }, "#!/usr/bin/env node\nvar x = 1;");

        var exception = Record.Exception(() => parser.Parse());

        Assert.Null(exception);
    }

    [Fact]
    public void ParserShouldSkipHashbangByDefault()
    {
        var parser = new Parser(new Options(), "#!/usr/bin/env node\nvar x = 1;");

        var exception = Record.Exception(() => parser.Parse());

        Assert.Null(exception);
    }

    [Fact]
    public void ParserShouldTreatHashbangAsErrorWhenDisabled()
    {
        var parser = new Parser(new Options { AllowHashBang = false }, "#!/usr/bin/env node\nvar x = 1;");

        var exception = Assert.Throws<SyntaxError>(() => parser.Parse());

        Assert.Contains("Unexpected character", exception.Message);
    }

    [Fact]
    public void ParserShouldPreserveComputedClassFieldsWhenPrinting()
    {
        var testData = new ParserTestData
        {
            SourceName = "computed-class-fields.js",
            Input = "class Foo { [x] = 1; static [y] = 2; }"
        };

        var (_, outMinJs, _, outNiceJs, _) = ParseTestCore(testData);

        Assert.StartsWith("class Foo{[x]=1;static[y]=2;}", outMinJs);
        Assert.Contains("[x] = 1;", outNiceJs);
        Assert.Contains("static [y] = 2;", outNiceJs);
    }

    [Fact]
    public void ParserShouldTreatComputedClassFieldKeysAsReads()
    {
        var testData = new ParserTestData
        {
            SourceName = "computed-class-fields.js",
            Input = "class Foo { [x] = 1; }"
        };

        var (outAst, _, _, _, _) = ParseTestCore(testData);

        Assert.Contains("SymbolRef 1:14 - 1:15 [Read]", outAst);
    }

    [Fact]
    public void ParserShouldPreserveRegexpUnicodeSetsFlagWhenPrinting()
    {
        var testData = new ParserTestData
        {
            SourceName = "regexp-v-flag.js",
            Input = "/[a&&b]/v;"
        };

        var (outAst, outMinJs, _, outNiceJs, _) = ParseTestCore(testData);

        Assert.Contains("Flags: UnicodeSets", outAst);
        Assert.StartsWith("/[a&&b]/v", outMinJs);
        Assert.Contains("/[a&&b]/v;", outNiceJs);
    }

    [Fact]
    public void ParserShouldPreserveRegexpBraceCharactersWhenPrinting()
    {
        var testData = new ParserTestData
        {
            SourceName = "regexp-braces.js",
            Input = "const close = /}/g; const open = /{/g; const escaped = /\\}/;"
        };

        var (_, outMinJs, _, outNiceJs, _) = ParseTestCore(testData);

        Assert.StartsWith("const close=/}/g;const open=/{/g;const escaped=/\\}/", outMinJs);
        Assert.Contains("const close = /}/g;", outNiceJs);
        Assert.Contains("const open = /{/g;", outNiceJs);
        Assert.Contains("const escaped = /\\}/;", outNiceJs);
    }

    [Fact]
    public void ParserShouldPreserveImportAttributesWhenPrinting()
    {
        var testData = new ParserTestData
        {
            SourceName = "module-import-attributes.js",
            Input = """
                    import data from "./data.json" with { type: "json" };
                    export { data } from "./data.json" with { type: "json" };
                    import("./data.json", { with: { type: "json" } });
                    """
        };

        var (_, outMinJs, _, outNiceJs, _) = ParseTestCore(testData);

        Assert.Contains("import data from\"./data.json\"with{type:\"json\"}", outMinJs);
        Assert.Contains("export{data}from\"./data.json\"with{type:\"json\"}", outMinJs);
        Assert.Contains("import(\"./data.json\",{with:{type:\"json\"}})", outMinJs);
        Assert.Contains("import data from \"./data.json\" with", outNiceJs);
        Assert.Contains("export { data } from \"./data.json\" with", outNiceJs);
    }

    [Fact]
    public void ParserShouldPreserveImportDeferWhenPrinting()
    {
        var testData = new ParserTestData
        {
            SourceName = "module-import-defer.js",
            Input = """
                    import defer featureDefault from "./feature-default";
                    import defer * as feature from "./feature";
                    import defer { value } from "./values";
                    import defer from "./normal-default";
                    """
        };

        var (outAst, outMinJs, _, outNiceJs, _) = ParseTestCore(testData);

        Assert.Contains("import defer featureDefault from\"./feature-default\"", outMinJs);
        Assert.Contains("import defer*as feature from\"./feature\"", outMinJs);
        Assert.Contains("import defer{value}from\"./values\"", outMinJs);
        Assert.Contains("import defer from\"./normal-default\"", outMinJs);
        Assert.Contains("Name: defer", outAst);
        Assert.Contains("import defer featureDefault from \"./feature-default\";", outNiceJs);
        Assert.Contains("import defer from \"./normal-default\";", outNiceJs);
    }

    [Fact]
    public void ParserShouldParseAndPrintBasicJsx()
    {
        var testData = new ParserTestData
        {
            SourceName = "simple-jsx.js",
            Input = "const x = <div id=\"a\">Hi</div>;"
        };

        var (outAst, outMinJs, _, outNiceJs, _) = ParseTestCore(testData);

        Assert.Contains("JsxElement", outAst);
        Assert.StartsWith("const x=<div id=\"a\">Hi</div>", outMinJs);
        Assert.Contains("const x = <div id=\"a\">Hi</div>;", outNiceJs);
    }

    [Fact]
    public void ParserShouldParseAndPrintJsxAttributesAndExpressions()
    {
        var testData = new ParserTestData
        {
            SourceName = "jsx-attributes.js",
            Input = "const view = <Foo.Bar id=\"x\" count={n} disabled {...props}>{label}{...items}</Foo.Bar>;"
        };

        var (_, outMinJs, _, outNiceJs, _) = ParseTestCore(testData);

        Assert.Contains("<Foo.Bar id=\"x\" count={n} disabled {...props}>{label}{...items}</Foo.Bar>", outMinJs);
        Assert.Contains("<Foo.Bar id=\"x\" count={n} disabled {...props}>{label}{...items}</Foo.Bar>", outNiceJs);
    }

    [Fact]
    public void ParserShouldParseAndPrintJsxFragmentsAndNamespacedNames()
    {
        var testData = new ParserTestData
        {
            SourceName = "jsx-fragment.js",
            Input = "const f = <><svg:path data-id=\"1\" /></>;"
        };

        var (outAst, outMinJs, _, outNiceJs, _) = ParseTestCore(testData);

        Assert.Contains("JsxFragment", outAst);
        Assert.Contains("<><svg:path data-id=\"1\" /></>", outMinJs);
        Assert.Contains("<><svg:path data-id=\"1\" /></>;", outNiceJs);
    }

    [Fact]
    public void ParserShouldRejectMismatchedJsxClosingTag()
    {
        var testData = new ParserTestData
        {
            SourceName = "jsx-mismatch.js",
            Input = "const x = <div></span>;"
        };

        var (outAst, _, _, _, _) = ParseTestCore(testData);

        Assert.Contains("Expected closing JSX tag div", outAst);
    }
}
