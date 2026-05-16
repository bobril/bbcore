using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Njsast;
using Njsast.Ast;
using Njsast.AstDump;
using Njsast.Bobril;
using Njsast.Output;
using Njsast.Reader;
using Njsast.SourceMap;
using Njsast.Utils;
using Xunit;

namespace Test.TypeScript;

public sealed class TypeScriptParserTest
{
    [Theory]
    [TypeScriptParserTestDataProvider("Input/TypeScript/Parser")]
    public void TypeScriptParserShouldProduceExpectedNjsastOutput(TypeScriptParserTestData testData)
    {
        var (outAst, outNiceJs, outNiceJsMap, outMinJs, outMinJsMap) = TypeScriptParserTestCore(testData);

        if (testData.ExpectedAst.Length != 0)
            Assert.Equal(testData.ExpectedAst, outAst);
        Assert.Equal(testData.ExpectedNiceJs, outNiceJs);
        if (testData.ExpectedNiceJsMap != null)
            Assert.Equal(testData.ExpectedNiceJsMap, outNiceJsMap);
        Assert.Equal(testData.ExpectedMinJs, outMinJs);
        if (testData.ExpectedMinJsMap != null)
            Assert.Equal(testData.ExpectedMinJsMap, outMinJsMap);
    }

    [Fact]
    public void TypeScriptParserShouldLowerAutoAccessorDecorators()
    {
        var input = File.ReadAllText("Input/TypeScript/UnsupportedDecorators/stage3-accessor.ts");

        var output = PrintTypeScript(input);

        Assert.Contains("#name_accessor_storage = \"ready\";", output);
        Assert.Contains("get name()", output);
        Assert.Contains("__decorate([ logged ], Service.prototype, \"name\", null);", output);
    }

    [Fact]
    public void TypeScriptParserShouldLowerAutoAccessors()
    {
        var input = """
            class Service {
                accessor value = 1;
                accessor #privateValue;
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("#value_accessor_storage = 1;", output);
        Assert.Contains("get value()", output);
        Assert.Contains("return this.#value_accessor_storage;", output);
        Assert.Contains("set value(value)", output);
        Assert.Contains("this.#value_accessor_storage = value;", output);
        Assert.Contains("#privateValue_accessor_storage;", output);
        Assert.Contains("get #privateValue()", output);
        Assert.Contains("set #privateValue(value)", output);
    }

    [Fact]
    public void TypeScriptParserShouldLowerStaticAutoAccessors()
    {
        var input = """
            class Service {
                static accessor value = 1;
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("static #value_accessor_storage = 1;", output);
        Assert.Contains("static get value()", output);
        Assert.Contains("return Service.#value_accessor_storage;", output);
        Assert.Contains("static set value(value)", output);
        Assert.Contains("Service.#value_accessor_storage = value;", output);
    }

    [Fact]
    public void TypeScriptParserShouldLowerAutoAccessorsWithMixedModifiers()
    {
        var input = """
            class Service {
                public static readonly accessor value: number = 1;
                static override accessor other = 2;
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("static #value_accessor_storage = 1;", output);
        Assert.Contains("static get value()", output);
        Assert.Contains("Service.#value_accessor_storage", output);
        Assert.Contains("static #other_accessor_storage = 2;", output);
        Assert.Contains("static get other()", output);
        Assert.DoesNotContain("static readonly;", output);
        Assert.DoesNotContain("static override;", output);
    }

    [Fact]
    public void TypeScriptParserShouldPreserveStaticOnFieldsWithTypeScriptModifiers()
    {
        var input = """
            class Service {
                static readonly #privateValue = 1;
                static readonly [key] = 2;
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("static #privateValue = 1;", output);
        Assert.Contains("static [key] = 2;", output);
        Assert.DoesNotContain("static readonly;", output);
    }

    [Fact]
    public void TypeScriptParserShouldTreatBareAccessorAsClassElementNameLikeTypeScript60()
    {
        var input = """
            class Service {
                accessor = 1;
                static accessor = 2;
                static readonly accessor = 3;
                accessor() {
                    return this.accessor;
                }
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("accessor = 1;", output);
        Assert.Contains("static accessor = 2;", output);
        Assert.Contains("static accessor = 3;", output);
        Assert.Contains("accessor()", output);
        Assert.DoesNotContain("_accessor_storage", output);
    }

    [Fact]
    public void TypeScriptParserShouldLowerDefaultExportedStaticAutoAccessors()
    {
        var input = """
            export default class {
                static accessor value = 1;
                static accessor [key()] = 2;
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("var _a;", output);
        Assert.Contains("export default class default_1", output);
        Assert.Contains("static #value_accessor_storage = 1;", output);
        Assert.Contains("return default_1.#value_accessor_storage;", output);
        Assert.Contains("static #_a_accessor_storage = 2;", output);
        Assert.Contains("static get [_a = key()]()", output);
        Assert.Contains("return default_1.#_a_accessor_storage;", output);
    }

    [Fact]
    public void TypeScriptParserShouldLowerNamedClassExpressionStaticAutoAccessors()
    {
        var input = """
            const Service = class NamedService {
                static accessor value = 1;
            };
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("const Service = class NamedService", output);
        Assert.Contains("static #value_accessor_storage = 1;", output);
        Assert.Contains("return NamedService.#value_accessor_storage;", output);
        Assert.Contains("NamedService.#value_accessor_storage = value;", output);
    }

    [Fact]
    public void TypeScriptParserShouldLowerAnonymousClassExpressionStaticAutoAccessors()
    {
        var input = """
            const Service = class {
                static accessor value = 1;
                static accessor [key()] = 2;
            };
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("var _a, _b;", output);
        Assert.Contains("static #value_accessor_storage = 1;", output);
        Assert.Contains("return _a.#value_accessor_storage;", output);
        Assert.Contains("_a.#value_accessor_storage = value;", output);
        Assert.Contains("static #_a_accessor_storage = 2;", output);
        Assert.Contains("static get [_b = key()]()", output);
        Assert.Contains("return _a.#_a_accessor_storage;", output);
        Assert.Contains("_a.#_a_accessor_storage = value;", output);
    }

    [Fact]
    public void TypeScriptParserShouldLowerComputedAutoAccessors()
    {
        var input = """
            class Service {
                accessor [key()] = 1;
                static accessor [staticKey()] = 2;
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("var _a, _b;", output);
        Assert.Contains("#_a_accessor_storage = 1;", output);
        Assert.Contains("get [_a = key()]()", output);
        Assert.Contains("set [_a](value)", output);
        Assert.Contains("static #_b_accessor_storage = 2;", output);
        Assert.Contains("static get [_b = staticKey()]()", output);
        Assert.Contains("static set [_b](value)", output);
    }

    [Fact]
    public void TypeScriptParserShouldLowerLiteralAutoAccessorStorageNamesLikeTypeScript60()
    {
        var input = """
            const key = "computed";
            class Service {
                accessor "literal" = 1;
                accessor value = 2;
                accessor 0 = 3;
                accessor [key] = 4;
                static accessor "staticLiteral" = 5;
                static accessor ["computedLiteral"] = 6;
            }
            use(Service);
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Contains("#_a_accessor_storage = 1;", oracleJs);
        Assert.Contains("#_a_accessor_storage = 1;", actualJs);
        Assert.Contains("#value_accessor_storage = 2;", actualJs);
        Assert.Contains("#_b_accessor_storage = 3;", actualJs);
        Assert.Contains("#_c_accessor_storage = 4;", actualJs);
        Assert.Contains("static #_d_accessor_storage = 5;", actualJs);
        Assert.Contains("static #_e_accessor_storage = 6;", actualJs);
        Assert.DoesNotContain("_b = \"computedLiteral\"", actualJs);
        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(actualJs));
    }

    [Fact]
    public void TypeScriptParserShouldPreserveComputedLiteralAutoAccessorKeysLikeTypeScript60()
    {
        var input = """
            class Service {
                accessor [0] = 1;
                accessor [`literal`] = 2;
                accessor [`value-${suffix}`] = 3;
            }
            use(Service);
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(actualJs));
        Assert.Contains("get [0]()", actualJs);
        Assert.Contains("get [`literal`]()", actualJs);
        Assert.Contains("get [_a = `value-${suffix}`]()", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldLowerComputedAutoAccessorDecorators()
    {
        var input = """
            class Service {
                @field accessor [key()] = 1;
                @field static accessor [staticKey()] = 2;
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("var _a, _b;", output);
        Assert.Contains("get [_a = key()]()", output);
        Assert.Contains("static get [_b = staticKey()]()", output);
        Assert.Contains("__decorate([ field ], Service.prototype, _a, null);", output);
        Assert.Contains("__decorate([ field ], Service, _b, null);", output);
    }

    [Fact]
    public void TypeScriptParserShouldIgnorePrivateMemberDecoratorsLikeTypeScript60()
    {
        var input = """
            class Service {
                @field #value = 1;
                @field accessor #ready = true;
                @field static #staticValue = 2;
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("#value = 1;", output);
        Assert.Contains("#ready_accessor_storage = true;", output);
        Assert.Contains("static #staticValue = 2;", output);
        Assert.DoesNotContain("__decorate", output);
    }

    [Fact]
    public void TypeScriptParserShouldEraseAnonymousClassExpressionAutoAccessorDecorators()
    {
        var input = """
            const Service = class {
                @field accessor value = 1;
                @field static accessor [key()] = 2;
            };
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("const Service = class", output);
        Assert.Contains("get value()", output);
        Assert.Contains("static get [_b = key()]()", output);
        Assert.DoesNotContain("__decorate", output);
    }

    [Fact]
    public void TypeScriptParserShouldLowerDefaultExportedAutoAccessorDecorators()
    {
        var input = """
            export default class {
                @field accessor value = 1;
                @field static accessor [key()] = 2;
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("export default class default_1", output);
        Assert.Contains("__decorate([ field ], default_1.prototype, \"value\", null);", output);
        Assert.Contains("__decorate([ field ], default_1, _a, null);", output);
    }

    [Fact]
    public void TypeScriptParserShouldHoistComputedAutoAccessorTempsFromNestedBlocks()
    {
        var input = """
            if (enabled) {
                class Service {
                    accessor [key()] = 1;
                }
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("var _a;\n\nif (enabled)", output);
        Assert.Contains("get [_a = key()]()", output);
    }

    [Fact]
    public void TypeScriptParserShouldLowerComputedAutoAccessorsInsideFunctions()
    {
        var input = """
            function make() {
                return class {
                    accessor [key()] = 1;
                };
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("function make()", output);
        Assert.Contains("var _a;", output);
        Assert.Contains("return class", output);
        Assert.Contains("get [_a = key()]()", output);
    }

    [Fact]
    public void TypeScriptParserShouldLowerUsingDeclarations()
    {
        var input = """
            using resource = make();
            using second = makeSecond();
            after(resource);
            async function disposeLater() {
                await using asyncResource = makeAsync();
                using syncResource = makeSync();
                after(asyncResource);
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("var __addDisposableResource =", output);
        Assert.Contains("var __disposeResources =", output);
        Assert.Contains("var resource, second;", output);
        Assert.Contains("const env_1 = {", output);
        Assert.Contains("resource = __addDisposableResource(env_1, make(), false);", output);
        Assert.Contains("second = __addDisposableResource(env_1, makeSecond(), false);", output);
        Assert.Contains("after(resource);", output);
        Assert.Contains("__disposeResources(env_1);", output);
        Assert.Contains("const asyncResource = __addDisposableResource(env_2, makeAsync(), true);", output);
        Assert.Contains("const syncResource = __addDisposableResource(env_2, makeSync(), false);", output);
        Assert.Contains("after(asyncResource);", output);
        Assert.Contains("const result_1 = __disposeResources(env_2);", output);
        Assert.Contains("await result_1;", output);
    }

    [Fact]
    public void TypeScriptParserShouldLowerExportUsingDeclarationsLikeTypeScript60()
    {
        var input = """
            export using resource = getResource();
            console.log(resource);
            export await using asyncResource = getAsyncResource();
            console.log(asyncResource);
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("var resource, asyncResource;", output);
        Assert.Contains("resource = __addDisposableResource", output);
        Assert.Contains("asyncResource = __addDisposableResource", output);
        Assert.Contains("console.log(resource);", output);
        Assert.Contains("console.log(asyncResource);", output);
        Assert.Contains("__disposeResources(env_", output);
        Assert.Contains("await result_", output);
        Assert.DoesNotContain("export using", output);
        Assert.DoesNotContain("export await using", output);
        Assert.DoesNotContain("export var resource", output);
    }

    [Fact]
    public void TypeScriptParserShouldKeepTopLevelImportsOutsideUsingScopeLikeTypeScript60()
    {
        var input = """
            await using value = acquireAsync();
            import { x } from "./x";
            use(value, x);
            """;

        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");
        var actualJs = PrintTypeScript(input);

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs, removeTypeScriptHelpers: true),
            DumpJavaScriptAstWithoutPositions(actualJs, removeTypeScriptHelpers: true));
        Assert.True(actualJs.IndexOf("import { x }", StringComparison.Ordinal) <
                    actualJs.IndexOf("var value;", StringComparison.Ordinal));
    }

    [Fact]
    public void TypeScriptParserShouldSplitTopLevelExportDeclarationsOutsideUsingScopeLikeTypeScript60()
    {
        var input = """
            await using value = acquireAsync();
            export const y = use(value);
            export { value };
            use(y);
            """;

        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");
        var actualJs = PrintTypeScript(input);

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs, removeTypeScriptHelpers: true),
            DumpJavaScriptAstWithoutPositions(actualJs, removeTypeScriptHelpers: true));
        Assert.True(actualJs.IndexOf("export { value }", StringComparison.Ordinal) <
                    actualJs.IndexOf("var value;", StringComparison.Ordinal));
        Assert.True(actualJs.IndexOf("var value;", StringComparison.Ordinal) <
                    actualJs.IndexOf("export let y;", StringComparison.Ordinal));
        Assert.Contains("y = use(value);", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldSplitTopLevelExportedClassesOutsideUsingScopeLikeTypeScript60()
    {
        var input = """
            await using value = acquireAsync();
            export class C {
                method() {
                    return value;
                }
            }
            use(C);
            """;

        var oracleJs = TranspileWithTypeScript60(input);
        var actualJs = PrintTypeScript(input);

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs, removeTypeScriptHelpers: true),
            DumpJavaScriptAstWithoutPositions(actualJs, removeTypeScriptHelpers: true));
        Assert.True(actualJs.IndexOf("export { C }", StringComparison.Ordinal) <
                    actualJs.IndexOf("var value, C;", StringComparison.Ordinal));
        Assert.Contains("C = class C", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldKeepTopLevelExportedFunctionsOutsideUsingScopeLikeTypeScript60()
    {
        var input = """
            await using value = acquireAsync();
            export function f() {
                return value;
            }
            use(f);
            """;

        var oracleJs = TranspileWithTypeScript60(input);
        var actualJs = PrintTypeScript(input);

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs, removeTypeScriptHelpers: true),
            DumpJavaScriptAstWithoutPositions(actualJs, removeTypeScriptHelpers: true));
        Assert.True(actualJs.IndexOf("export function f()", StringComparison.Ordinal) <
                    actualJs.IndexOf("var value;", StringComparison.Ordinal));
    }

    [Fact]
    public void TypeScriptParserShouldSplitTopLevelDefaultExportsOutsideUsingScopeLikeTypeScript60()
    {
        var input = """
            await using value = acquireAsync();
            export default use(value);
            """;

        var oracleJs = TranspileWithTypeScript60(input);
        var actualJs = PrintTypeScript(input);

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs, removeTypeScriptHelpers: true),
            DumpJavaScriptAstWithoutPositions(actualJs, removeTypeScriptHelpers: true));
        Assert.True(actualJs.IndexOf("export { _default as default }", StringComparison.Ordinal) <
                    actualJs.IndexOf("var value, _default;", StringComparison.Ordinal));
        Assert.Contains("_default = use(value);", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldSplitTopLevelDefaultExportedClassesOutsideUsingScopeLikeTypeScript60()
    {
        var input = """
            await using value = acquireAsync();
            export default class C {
                method() {
                    return value;
                }
            }
            use(C);
            """;

        var oracleJs = TranspileWithTypeScript60(input);
        var actualJs = PrintTypeScript(input);

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs, removeTypeScriptHelpers: true),
            DumpJavaScriptAstWithoutPositions(actualJs, removeTypeScriptHelpers: true));
        Assert.True(actualJs.IndexOf("export { _default as default }", StringComparison.Ordinal) <
                    actualJs.IndexOf("var value, C, _default;", StringComparison.Ordinal));
        Assert.Contains("_default = C = class C", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldNameTopLevelAnonymousDefaultExportedClassesOutsideUsingScopeLikeTypeScript60()
    {
        var input = """
            await using value = acquireAsync();
            export default class {
                method() {
                    return value;
                }
            }
            """;

        var oracleJs = TranspileWithTypeScript60(input);
        var actualJs = PrintTypeScript(input);

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs, removeTypeScriptHelpers: true),
            DumpJavaScriptAstWithoutPositions(actualJs, removeTypeScriptHelpers: true));
        Assert.Contains("var __setFunctionName =", actualJs);
        Assert.Contains("__setFunctionName(this, \"default\");", actualJs);
        Assert.Contains("_default = class", actualJs);
        Assert.DoesNotContain("_default = default_1 = class default_1", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldUseSyntheticNameForTopLevelAnonymousDefaultExportedStaticClassesLikeTypeScript60()
    {
        var input = """
            await using value = acquireAsync();
            export default class {
                static field = value;
                static accessor current = value;
            }
            """;

        var oracleJs = TranspileWithTypeScript60(input);
        var actualJs = PrintTypeScript(input);

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs, removeTypeScriptHelpers: true),
            DumpJavaScriptAstWithoutPositions(actualJs, removeTypeScriptHelpers: true));
        Assert.Contains("_default = default_1 = class default_1", actualJs);
        Assert.DoesNotContain("__setFunctionName(this, \"default\")", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldSplitTopLevelExportedEnumsOutsideUsingScopeLikeTypeScript60()
    {
        var input = """
            await using value = acquireAsync();
            export enum E {
                A = use(value),
                B = 2
            }
            use(E);
            """;

        var oracleJs = TranspileWithTypeScript60(input);
        var actualJs = PrintTypeScript(input);

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs, removeTypeScriptHelpers: true),
            DumpJavaScriptAstWithoutPositions(actualJs, removeTypeScriptHelpers: true));
        Assert.True(actualJs.IndexOf("export { E }", StringComparison.Ordinal) <
                    actualJs.IndexOf("var value, E;", StringComparison.Ordinal));
        Assert.True(actualJs.IndexOf("var value, E;", StringComparison.Ordinal) <
                    actualJs.IndexOf("(function(E)", StringComparison.Ordinal));
    }

    [Fact]
    public void TypeScriptParserShouldSplitTopLevelExportedNamespacesOutsideUsingScopeLikeTypeScript60()
    {
        var input = """
            await using value = acquireAsync();
            export namespace N {
                export const x = value;
            }
            use(N);
            """;

        var oracleJs = TranspileWithTypeScript60(input);
        var actualJs = PrintTypeScript(input);

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs, removeTypeScriptHelpers: true),
            DumpJavaScriptAstWithoutPositions(actualJs, removeTypeScriptHelpers: true));
        Assert.True(actualJs.IndexOf("export { N }", StringComparison.Ordinal) <
                    actualJs.IndexOf("var value, N;", StringComparison.Ordinal));
        Assert.Contains("N.x = value;", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldEraseTopLevelTypeOnlyExportsAfterUsingScopeLikeTypeScript60()
    {
        var input = """
            await using value = acquireAsync();
            export interface I {
                x: typeof value;
            }
            export type T = typeof value;
            use(value);
            """;

        var oracleJs = TranspileWithTypeScript60(input);
        var actualJs = PrintTypeScript(input);

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs, removeTypeScriptHelpers: true),
            DumpJavaScriptAstWithoutPositions(actualJs, removeTypeScriptHelpers: true));
        Assert.EndsWith("export { };", actualJs.TrimEnd());
        Assert.DoesNotContain("interface I", actualJs);
        Assert.DoesNotContain("type T", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldKeepTopLevelReExportsOutsideUsingScopeLikeTypeScript60()
    {
        var input = """
            await using value = acquireAsync();
            export * from "./mod";
            export { x as y } from "./other";
            use(value);
            """;

        var oracleJs = TranspileWithTypeScript60(input);
        var actualJs = PrintTypeScript(input);

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs, removeTypeScriptHelpers: true),
            DumpJavaScriptAstWithoutPositions(actualJs, removeTypeScriptHelpers: true));
        Assert.True(actualJs.IndexOf("export * from", StringComparison.Ordinal) <
                    actualJs.IndexOf("var value;", StringComparison.Ordinal));
        Assert.True(actualJs.IndexOf("export { x as y }", StringComparison.Ordinal) <
                    actualJs.IndexOf("var value;", StringComparison.Ordinal));
    }

    [Fact]
    public void TypeScriptParserShouldEraseTopLevelTypeOnlyReExportsAfterUsingScopeLikeTypeScript60()
    {
        var input = """
            await using value = acquireAsync();
            export type { T } from "./types";
            use(value);
            """;

        var oracleJs = TranspileWithTypeScript60(input);
        var actualJs = PrintTypeScript(input);

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs, removeTypeScriptHelpers: true),
            DumpJavaScriptAstWithoutPositions(actualJs, removeTypeScriptHelpers: true));
        Assert.EndsWith("export { };", actualJs.TrimEnd());
        Assert.DoesNotContain("from \"./types\"", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldEraseTopLevelTypeOnlyImportsAfterUsingScopeLikeTypeScript60()
    {
        var input = """
            await using value = acquireAsync();
            import type { T } from "./types";
            use(value);
            """;

        var oracleJs = TranspileWithTypeScript60(input);
        var actualJs = PrintTypeScript(input);

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs, removeTypeScriptHelpers: true),
            DumpJavaScriptAstWithoutPositions(actualJs, removeTypeScriptHelpers: true));
        Assert.EndsWith("export { };", actualJs.TrimEnd());
        Assert.DoesNotContain("from \"./types\"", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldEraseTopLevelImportEqualsAfterUsingScopeLikeTypeScript60()
    {
        var input = """
            await using value = acquireAsync();
            import fs = require("fs");
            use(value, fs);
            """;

        var oracleJs = TranspileWithTypeScript60(input);
        var actualJs = PrintTypeScript(input);

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs, removeTypeScriptHelpers: true),
            DumpJavaScriptAstWithoutPositions(actualJs, removeTypeScriptHelpers: true));
        Assert.EndsWith("export { };", actualJs.TrimEnd());
        Assert.DoesNotContain("import fs", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldEraseTopLevelAmbientDeclarationsAfterUsingScopeLikeTypeScript60()
    {
        var input = """
            await using value = acquireAsync();
            declare global {
                interface Window {
                    x: typeof value;
                }
            }
            declare namespace N {
                export interface X {}
            }
            use(value);
            """;

        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");
        var actualJs = PrintTypeScript(input);

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs, removeTypeScriptHelpers: true),
            DumpJavaScriptAstWithoutPositions(actualJs, removeTypeScriptHelpers: true));
        Assert.DoesNotContain("declare global", actualJs);
        Assert.DoesNotContain("namespace N", actualJs);
        Assert.DoesNotContain("export { }", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldEraseTopLevelExportDeclareAfterUsingScopeLikeTypeScript60()
    {
        var input = """
            await using value = acquireAsync();
            export declare class C {
                x: typeof value;
            }
            use(value);
            """;

        var oracleJs = TranspileWithTypeScript60(input);
        var actualJs = PrintTypeScript(input);

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs, removeTypeScriptHelpers: true),
            DumpJavaScriptAstWithoutPositions(actualJs, removeTypeScriptHelpers: true));
        Assert.EndsWith("export { };", actualJs.TrimEnd());
        Assert.DoesNotContain("declare class C", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldLowerTopLevelExportImportAfterUsingScopeLikeTypeScript60()
    {
        var input = """
            namespace NS {
                export const x = 1;
            }
            await using value = acquireAsync();
            export import x = NS.x;
            use(value, x);
            """;

        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");
        var actualJs = PrintTypeScript(input);

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs, removeTypeScriptHelpers: true),
            DumpJavaScriptAstWithoutPositions(actualJs, removeTypeScriptHelpers: true));
        Assert.Contains("export let x;", actualJs);
        Assert.Contains("x = NS.x;", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldSplitTopLevelMixedExportSpecifiersAfterUsingScopeLikeTypeScript60()
    {
        var input = """
            await using value = acquireAsync();
            type T = typeof value;
            const x = value;
            export { type T, x };
            use(x);
            """;

        var oracleJs = TranspileWithTypeScript60(input);
        var actualJs = PrintTypeScript(input);

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs, removeTypeScriptHelpers: true),
            DumpJavaScriptAstWithoutPositions(actualJs, removeTypeScriptHelpers: true));
        Assert.True(actualJs.IndexOf("export { x }", StringComparison.Ordinal) <
                    actualJs.IndexOf("var value, x;", StringComparison.Ordinal));
        Assert.Contains("x = value;", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldLowerNamespaceExportUsingDeclarationsLikeTypeScript60()
    {
        var input = """
            namespace N {
                export using resource = getResource();
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("N.resource = getResource();", output);
        Assert.DoesNotContain("__addDisposableResource", output);
        Assert.DoesNotContain("export using", output);
    }

    [Fact]
    public void TypeScriptParserShouldLowerNamespaceUsingScopeExportsLikeTypeScript60()
    {
        var input = """
            namespace Runtime {
                using resource = acquire();
                export const value = 1;
                resource.use();
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("const env_1 = {", output);
        Assert.Contains("resource = __addDisposableResource(env_1, acquire(), false);", output);
        Assert.Contains("Runtime.value = 1;", output);
        Assert.Contains("resource.use();", output);
        Assert.Contains("__disposeResources(env_1);", output);
        Assert.DoesNotContain("export const value", output);
    }

    [Fact]
    public void TypeScriptParserShouldEmitUsingHelpersOnceForNamespaceUsingLikeTypeScript60()
    {
        var input = """
            namespace Runtime {
                using resource = acquire();
                resource.use();
            }
            using later = acquireLater();
            later.use();
            """;

        var output = PrintTypeScript(input);

        Assert.Equal(1, CountOccurrences(output, "var __addDisposableResource ="));
        Assert.Equal(1, CountOccurrences(output, "var __disposeResources ="));
        Assert.Contains("var __addDisposableResource = this && this.__addDisposableResource", output);
        Assert.Contains("var Runtime;", output);
        Assert.True(output.IndexOf("var __addDisposableResource =", StringComparison.Ordinal) <
                    output.IndexOf("var Runtime;", StringComparison.Ordinal));
        Assert.DoesNotContain("(function(Runtime) {\n    var __addDisposableResource =", output);
        Assert.Contains("const resource = __addDisposableResource(env_2, acquire(), false);", output);
        Assert.Contains("later = __addDisposableResource(env_1, acquireLater(), false);", output);
    }

    [Fact]
    public void TypeScriptParserShouldReserveTopLevelUsingTempsBeforeNestedScopesLikeTypeScript60()
    {
        var input = """
            async function before() {
                await using local = acquireBefore();
                return local;
            }
            namespace Before {
                using resource = acquireBeforeNamespace();
                resource.use();
            }
            using top = acquireTop();
            function after() {
                using local = acquireAfter();
                return local;
            }
            namespace After {
                using resource = acquireAfterNamespace();
                resource.use();
            }
            top.use();
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("const local = __addDisposableResource(env_2, acquireBefore(), true);", output);
        Assert.Contains("const result_1 = __disposeResources(env_2);", output);
        Assert.Contains("const resource = __addDisposableResource(env_3, acquireBeforeNamespace(), false);", output);
        Assert.Contains("top = __addDisposableResource(env_1, acquireTop(), false);", output);
        Assert.Contains("const local = __addDisposableResource(env_4, acquireAfter(), false);", output);
        Assert.Contains("const resource = __addDisposableResource(env_5, acquireAfterNamespace(), false);", output);
    }

    [Fact]
    public void TypeScriptParserShouldReserveTopLevelAwaitUsingResultTempBeforeNestedScopesLikeTypeScript60()
    {
        var input = """
            namespace Before {
                await using resource = acquireBeforeNamespace();
                resource.use();
            }
            await using top = acquireTop();
            async function after() {
                await using local = acquireAfter();
                return local;
            }
            namespace After {
                await using resource = acquireAfterNamespace();
                resource.use();
            }
            top.use();
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("const resource = __addDisposableResource(env_2, acquireBeforeNamespace(), true);", output);
        Assert.Contains("const result_2 = __disposeResources(env_2);", output);
        Assert.Contains("top = __addDisposableResource(env_1, acquireTop(), true);", output);
        Assert.Contains("const result_1 = __disposeResources(env_1);", output);
        Assert.Contains("const local = __addDisposableResource(env_3, acquireAfter(), true);", output);
        Assert.Contains("const result_3 = __disposeResources(env_3);", output);
        Assert.Contains("const resource = __addDisposableResource(env_4, acquireAfterNamespace(), true);", output);
        Assert.Contains("const result_4 = __disposeResources(env_4);", output);
    }

    [Fact]
    public void TypeScriptParserShouldReserveExportAwaitUsingTempsBeforeNestedScopesLikeTypeScript60()
    {
        var input = """
            function before() {
                using local = acquireBefore();
                return local;
            }
            export await using exported = acquireAsync();
            function after() {
                await using local = acquireAfter();
                return local;
            }
            exported.use();
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("const local = __addDisposableResource(env_2, acquireBefore(), false);", output);
        Assert.Contains("exported = __addDisposableResource(env_1, acquireAsync(), true);", output);
        Assert.Contains("const result_1 = __disposeResources(env_1);", output);
        Assert.Contains("const local = __addDisposableResource(env_3, acquireAfter(), true);", output);
        Assert.Contains("const result_2 = __disposeResources(env_3);", output);
    }

    [Fact]
    public void TypeScriptParserShouldEmitModuleMarkerForExportAwaitUsingLikeTypeScript60()
    {
        var input = """
            export await using exported = acquireAsync();
            exported.use();
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs, removeTypeScriptHelpers: true),
            DumpJavaScriptAstWithoutPositions(actualJs, removeTypeScriptHelpers: true));
        Assert.EndsWith("export { };", actualJs.TrimEnd());
    }

    [Fact]
    public void TypeScriptParserShouldNotReserveTopLevelUsingTempsForUsingIdentifierLikeTypeScript60()
    {
        var input = """
            const using = 1;
            using = 2;
            function read() {
                using local = acquire();
                return local;
            }
            console.log(using, read);
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("const using = 1;", output);
        Assert.Contains("using = 2;", output);
        Assert.Contains("const local = __addDisposableResource(env_1, acquire(), false);", output);
        Assert.DoesNotContain("env_2", output);
    }

    [Fact]
    public void TypeScriptParserShouldHoistNamespaceUsingExportedDestructuringTempsLikeTypeScript60()
    {
        var input = """
            namespace Runtime {
                using resource = acquire();
                export const { value = fallback } = source;
                export const [first, ...tail] = values;
                resource.use(value, first, tail);
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("var _a;\n    const env_1 = {", output);
        Assert.Contains("const resource = __addDisposableResource(env_1, acquire(), false);", output);
        Assert.Contains("_a = source.value, Runtime.value = _a === void 0 ? fallback : _a;", output);
        Assert.Contains("Runtime.first = values[0], Runtime.tail = values.slice(1);", output);
        Assert.Contains("resource.use(Runtime.value, Runtime.first, Runtime.tail);", output);
        Assert.DoesNotContain("try {\n        const resource = __addDisposableResource(env_1, acquire(), false);\n        var _a;", output);
    }

    [Fact]
    public void TypeScriptParserShouldEvaluateNamespaceExportedDestructuringSourcesOnceLikeTypeScript60()
    {
        var input = """
            namespace Runtime {
                export const { a, b } = getObject();
                export const [first, second] = getArray();
                export const { nested: { x, y } } = getNestedObject();
                export const [[innerFirst, innerSecond]] = getNestedArray();
                export const { [key()]: computed, plain } = getComputedObject();
                export const { withDefault = fallback, afterDefault } = getDefaultObject();
                export const { restValue, ...objectRest } = getRestObject();
                export const [arrayDefault = fallback, afterArrayDefault] = getDefaultArray();
                export const [arrayHead, ...arrayRest] = getRestArray();
                export const { nestedDefault: { deepDefault = fallback, afterDeepDefault } } = getDeepDefaultObject();
                export const { nestedRest: { deepRestValue, ...deepRest }, afterDeepRest } = getDeepRestObject();
                export let { mutableA, mutableB } = getMutableObject();
                export var [varFirst, varSecond] = getVarArray();
                export const { erasedConst };
                export let [erasedLet];
                export var { erasedVar };
                console.log(a, b, first, second, x, y, innerFirst, innerSecond, computed, plain,
                    withDefault, afterDefault, restValue, objectRest, arrayDefault, afterArrayDefault,
                    arrayHead, arrayRest, deepDefault, afterDeepDefault, deepRestValue, deepRest, afterDeepRest,
                    mutableA, mutableB, varFirst, varSecond);
            }
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs, removeTypeScriptHelpers: true),
            DumpJavaScriptAstWithoutPositions(actualJs, removeTypeScriptHelpers: true));
    }

    [Fact]
    public void TypeScriptParserShouldPreserveTypeScriptDeclarationsWithoutInitializersLikeTypeScript60()
    {
        var input = """
            const value;
            const { a };
            let [first];
            var { rest: { nested } };
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("const value;", output);
        Assert.Contains("const {a};", output);
        Assert.Contains("let [first];", output);
        Assert.Contains("var {rest: {nested}};", output);
    }

    [Fact]
    public void TypeScriptParserShouldContinueFunctionUsingTempIndexesAfterNamespaceBodiesLikeTypeScript60()
    {
        var input = """
            namespace Runtime {
                await using resource = acquireAsync();
                resource.use();
            }
            async function laterScope() {
                await using later = acquireLater();
                later.use();
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("const env_1 = {", output);
        Assert.Contains("catch (e_1)", output);
        Assert.Contains("const result_1 = __disposeResources(env_1);", output);
        Assert.Contains("const env_2 = {", output);
        Assert.Contains("catch (e_2)", output);
        Assert.Contains("const result_2 = __disposeResources(env_2);", output);
        Assert.Contains("const later = __addDisposableResource(env_2, acquireLater(), true);", output);
    }

    [Fact]
    public void TypeScriptParserShouldLowerForUsingDeclarations()
    {
        var input = """
            for (using item of items) {
                item.use();
            }
            for (await using asyncItem: Disposable of asyncItems)
                asyncItem.use();
            async function iterateAsyncResources() {
                for await (using streamed of stream)
                    streamed.use();
                for await (await using asyncStreamed of asyncStream)
                    asyncStreamed.use();
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("var __addDisposableResource =", output);
        Assert.Contains("var __disposeResources =", output);
        Assert.Contains("for (const item_1 of items)", output);
        Assert.Contains("const env_1 = {", output);
        Assert.Contains("const item = __addDisposableResource(env_1, item_1, false);", output);
        Assert.Contains("item.use();", output);
        Assert.Contains("__disposeResources(env_1);", output);
        Assert.Contains("for (const asyncItem_1 of asyncItems)", output);
        Assert.Contains("const asyncItem = __addDisposableResource(env_2, asyncItem_1, true);", output);
        Assert.Contains("asyncItem.use();", output);
        Assert.Contains("const result_1 = __disposeResources(env_2);", output);
        Assert.Contains("await result_1;", output);
        Assert.Contains("for await (const streamed_1 of stream)", output);
        Assert.Contains("const streamed = __addDisposableResource(env_3, streamed_1, false);", output);
        Assert.Contains("streamed.use();", output);
        Assert.Contains("for await (const asyncStreamed_1 of asyncStream)", output);
        Assert.Contains("const asyncStreamed = __addDisposableResource(env_4, asyncStreamed_1, true);", output);
        Assert.Contains("const result_2 = __disposeResources(env_4);", output);
        Assert.Contains("await result_2;", output);
        Parser.Parse(output, new Options { SourceType = SourceType.Module, EcmaVersion = 2022 });
    }

    [Fact]
    public void TypeScriptParserShouldLowerForUsingInitializerDeclarationsLikeTypeScript60()
    {
        var input = """
            for (using resource = acquire(); shouldContinue(); step())
                resource.use();
            async function disposeInForInitializer() {
                for (await using asyncResource: AsyncDisposable = acquireAsync(); shouldContinue(); step()) {
                    asyncResource.use();
                }
            }
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs, removeTypeScriptHelpers: true),
            DumpJavaScriptAstWithoutPositions(actualJs, removeTypeScriptHelpers: true));
    }

    [Fact]
    public void TypeScriptParserShouldPreserveForUsingInitializerDestructuringLikeTypeScript60()
    {
        var input = """
            for (using { value }: Disposable = acquire(); shouldContinue(); step())
                value.use();
            for (using first = acquireFirst(), { second } = acquireSecond(); shouldContinue(); step()) {
                first.use();
                second.use();
            }
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Contains("using { value } = acquire();", oracleJs);
        Assert.Contains("using first = acquireFirst(), { second } = acquireSecond();", oracleJs);
        Assert.Contains("using { value } = acquire();", actualJs);
        Assert.Contains("using first = acquireFirst(), { second } = acquireSecond();", actualJs);
        Assert.Contains("for (;shouldContinue(); step())", actualJs);
        Assert.Contains("__disposeResources", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldPreserveForUsingInDeclarationsLikeTypeScript60()
    {
        var input = """
            for (using key in object)
                console.log(key);
            for (using blockKey in object) {
                console.log(blockKey);
            }
            async function readAsyncKeys() {
                for await (await using asyncKey in object)
                    console.log(asyncKey);
                for await (using streamedKey in object) {
                    console.log(streamedKey);
                }
            }
            for (using trickyKey in object[")"]) {
                console.log(tag`${(() => { return trickyKey; })()}`);
            }
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Contains("for (using key in object)", oracleJs);
        Assert.Contains("for (using blockKey in object)", oracleJs);
        Assert.Contains("for (await using asyncKey in object)", oracleJs);
        Assert.Contains("for (using streamedKey in object)", oracleJs);
        Assert.Contains("for (using key in object)", actualJs);
        Assert.Contains("console.log(key);", actualJs);
        Assert.Contains("for (using blockKey in object) {\n    console.log(blockKey);\n}", actualJs);
        Assert.Contains("for (await using asyncKey in object)\n        console.log(asyncKey);", actualJs);
        Assert.Contains("for (using streamedKey in object) {\n        console.log(streamedKey);\n    }", actualJs);
        Assert.Contains("for (using trickyKey in object[\")\"])", actualJs);
        Assert.Contains("console.log(tag`${(() => { return trickyKey; })()}`);", actualJs);
        Assert.DoesNotContain("for await (await using asyncKey in object)", actualJs);
        Assert.DoesNotContain("for await (using streamedKey in object)", actualJs);
        Assert.DoesNotContain("__addDisposableResource", actualJs);
        Assert.DoesNotContain("__disposeResources", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldLowerForUsingDestructuringLikeTypeScript60()
    {
        var input = """
            for (using { value } of resources) {
                value.use();
            }
            for (await using { asyncValue }: AsyncDisposable of asyncResources)
                asyncValue.use();
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("for (const _a of resources)", output);
        Assert.Contains("using { value } = _a;", output);
        Assert.Contains("value.use();", output);
        Assert.Contains("for (const _a of asyncResources)", output);
        Assert.Contains("await using { asyncValue } = _a;", output);
        Assert.Contains("asyncValue.use();", output);
        Assert.Contains("__disposeResources", output);
    }

    [Fact]
    public void TypeScriptParserShouldLowerDestructuringUsingDeclarationsLikeTypeScript60()
    {
        var input = """
            using { value, nested: { inner = fallback } } = acquire();
            after(value, inner);
            {
                await using { asyncValue } = acquireAsync();
                after(asyncValue);
            }
            {
                using first = acquireFirst(), { second } = acquireSecond();
                after(first, second);
            }
            using topFirst = acquireTopFirst(), { topSecond } = acquireTopSecond();
            after(topFirst, topSecond);
            """;

        var output = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Contains("var value, inner, topFirst, topSecond;", output);
        Assert.Contains("({ value, nested: { inner = fallback } } = acquire());", output);
        Assert.Contains("await using { asyncValue } = acquireAsync();", output);
        Assert.Contains("using first = acquireFirst(), { second } = acquireSecond();", output);
        Assert.Contains("topFirst = acquireTopFirst(), { topSecond } = acquireTopSecond();", output);
        Assert.Contains("using first = acquireFirst(), { second } = acquireSecond();", oracleJs);
        Assert.Contains("topFirst = acquireTopFirst(), { topSecond } = acquireTopSecond();", oracleJs);
        Assert.DoesNotContain("const {asyncValue} = acquireAsync();", output);
        Assert.DoesNotContain("__addDisposableResource(env_1, acquire()", output);
        Assert.DoesNotContain("__addDisposableResource(env_2, acquireAsync()", output);
        Assert.Contains("const result_1 = __disposeResources(env_2);", output);
        Assert.Contains("await result_1;", output);
    }

    [Fact]
    public void TypeScriptParserShouldEmitOnlyDisposeHelperForPureDestructuringUsingLikeTypeScript60()
    {
        var input = """
            function read() {
                using { value } = acquire();
                value.use();
            }
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.DoesNotContain("var __addDisposableResource =", oracleJs);
        Assert.DoesNotContain("var __addDisposableResource =", actualJs);
        Assert.Contains("var __disposeResources =", actualJs);
        Assert.Contains("using { value } = acquire();", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldPreserveArrayUsingExpressionsLikeTypeScript60()
    {
        var input = """
            using [top] = acquire();
            after(top);
            function read() {
                using [local] = acquire();
                after(local);
            }
            for (using [item] = acquire(); ok(); step())
                after(item);
            for (using [entry] of values)
                after(entry);
            async function readAsync() {
                await using [asyncLocal] = acquireAsync();
                after(asyncLocal);
                for await (using [item] of values)
                    after(item);
                for await (await using [entry] in values)
                    after(entry);
            }
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Contains("using[top] = acquire();", oracleJs);
        Assert.Contains("using[top] = acquire();", actualJs);
        Assert.Contains("using[local] = acquire();", actualJs);
        Assert.Contains("for (using[item] = acquire(); ok(); step())", actualJs);
        Assert.Contains("for (using[entry] of values)", actualJs);
        Assert.Contains("await using[asyncLocal];", oracleJs);
        Assert.Contains("await using[asyncLocal];", actualJs);
        Assert.Contains("acquireAsync();", oracleJs);
        Assert.Contains("acquireAsync();", actualJs);
        Assert.Contains("for await (using[item] of values)", actualJs);
        Assert.Contains("for (await using[entry] in values)", actualJs);
        Assert.DoesNotContain("__addDisposableResource", actualJs);
        Assert.DoesNotContain("__disposeResources", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldPreserveSwitchCaseUsingDeclarationsLikeTypeScript60()
    {
        var input = """
            switch (kind) {
                case 1:
                    using resource = acquire();
                    resource.use();
                    break;
                default:
                    await using asyncResource = acquireAsync();
                    asyncResource.use();
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("case 1:\n    using resource = acquire();", output);
        Assert.Contains("default:\n    await using asyncResource = acquireAsync();", output);
        Assert.DoesNotContain("__addDisposableResource", output);
    }

    [Fact]
    public void TypeScriptParserShouldPreserveDirectBodyUsingDeclarationsLikeTypeScript60()
    {
        var input = """
            if (ok) using ifResource = acquire();
            while (ok) using whileResource = acquire();
            for (;;) await using loopResource = acquireAsync();
            label: using labeledResource = acquire();
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("if (ok) using ifResource = acquire();", output);
        Assert.Contains("while (ok) using whileResource = acquire();", output);
        Assert.Contains("for (;;) await using loopResource = acquireAsync();", output);
        Assert.Contains("label: using labeledResource = acquire();", output);
        Assert.DoesNotContain("__addDisposableResource", output);
    }

    [Fact]
    public void TypeScriptParserShouldPreserveUsingDeclarationsWithNestedTemplatesLikeTypeScript60()
    {
        var input = """
            switch (kind) {
                case 1:
                    using resource = tag`${(() => { return `${name}`; })()}`;
                    after(resource);
                    break;
            }
            if (ok) using direct = tag`${(() => { return `${name}`; })()}`;
            after(direct);
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("using resource = tag`${(() => { return `${name}`; })()}`;", output);
        Assert.Contains("if (ok) using direct = tag`${(() => { return `${name}`; })()}`;", output);
        Assert.Contains("after(direct);", output);
    }

    [Fact]
    public void TypeScriptParserShouldEraseForBindingTypeAnnotationsLikeTypeScript60()
    {
        var input = """
            for (const [key, value]: [string, number] of entries) {
                console.log(key, value);
            }
            for (const { x, y }: { x: number; y: number } of points) {
                console.log(x, y);
            }
            for (let index: number = 0; index < 3; index++) {
                console.log(index);
            }
            for (const key in object as Record<string, unknown>) {
                console.log(key);
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("for (const [key, value] of entries)", output);
        Assert.Contains("for (const {x, y} of points)", output);
        Assert.Contains("for (let index = 0; index < 3; index++)", output);
        Assert.Contains("for (const key in object)", output);
        Assert.DoesNotContain(": [string, number]", output);
        Assert.DoesNotContain(": {", output);
        Assert.DoesNotContain("Record<string", output);
    }

    [Fact]
    public void TypeScriptParserShouldEraseAsyncGenericArrowTypeParameters()
    {
        var input = """
            const one = async <T,>(value: T) => value;
            const two = async <T extends string>(value: T): Promise<T> => value;
            const three = async <const T>(value: T = fallback) => value;
            const four = async <T,>(value = ")") => value;
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("const one = async value => value;", output);
        Assert.Contains("const two = async value => value;", output);
        Assert.Contains("const three = async (value = fallback) => value;", output);
        Assert.Contains("const four = async (value = \")\") => value;", output);
        Assert.DoesNotContain("<T", output);
        Assert.DoesNotContain("Promise<T>", output);
    }

    [Fact]
    public void TypeScriptParserShouldEraseMappedTypesWithTemplateKeyRemapping()
    {
        var input = """
            type Renamed<T> = {
                readonly [K in keyof T as `prefix_${K & string}`]?: T[K];
            };
            const value = 1;
            """;

        var output = PrintTypeScript(input);

        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");
        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(output));
        Assert.Contains("const value = 1;", output);
    }

    [Fact]
    public void TypeScriptParserShouldLowerAccessorParameterProperties()
    {
        var input = """
            class Service {
                constructor(private readonly id: number, protected accessor value = 1) {}
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("id;", output);
        Assert.Contains("value;", output);
        Assert.Contains("constructor(id, value = 1)", output);
        Assert.Contains("this.id = id;", output);
        Assert.Contains("this.value = value;", output);
        Assert.DoesNotContain("accessor value", output);
    }

    [Fact]
    public void TypeScriptParserShouldLowerDecoratedParameterPropertiesLikeTypeScript60()
    {
        var input = """
            function param(target: unknown, key: string | undefined, index: number) {}

            class Service {
                constructor(@param public value: string, @param private readonly id = 1, @param protected accessor ready = true) {}
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("let Service = class Service {", output);
        Assert.Contains("value;", output);
        Assert.Contains("id;", output);
        Assert.Contains("ready;", output);
        Assert.Contains("constructor(value, id = 1, ready = true)", output);
        Assert.Contains("this.value = value;", output);
        Assert.Contains("this.id = id;", output);
        Assert.Contains("this.ready = ready;", output);
        Assert.Contains("Service = __decorate([ __param(0, param), __param(1, param), __param(2, param) ], Service);", output);
    }

    [Fact]
    public void TypeScriptParserShouldEraseDirectBodyTypeOnlyStatementsAsEmptyStatements()
    {
        var input = """
            if (ok) type IfType = string;
            while (ok) type WhileType = string;
            label: type LabelType = string;
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("if (ok) ;", output);
        Assert.Contains("while (ok) ;", output);
        Assert.Contains("label: ;", output);
        Assert.DoesNotContain("IfType", output);
        Assert.DoesNotContain("WhileType", output);
        Assert.DoesNotContain("LabelType", output);
    }

    [Fact]
    public void TypeScriptParserShouldLowerLocalEnumDeclarations()
    {
        var input = """
            function read() {
                enum Local {
                    A = 1,
                    B = A + 1
                }
                return Local.B;
            }
            class Service {
                static {
                    enum StaticLocal {
                        A = 1
                    }
                    console.log(StaticLocal.A);
                }
            }
            if (ok) enum Inline {
                A = 1
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("let Local;", output);
        Assert.Contains("Local[Local[\"B\"] = 2] = \"B\";", output);
        Assert.Contains("return Local.B;", output);
        Assert.Contains("let StaticLocal;", output);
        Assert.Contains("console.log(StaticLocal.A);", output);
        Assert.Contains("if (ok) {", output);
        Assert.Contains("var Inline;", output);
        Assert.Contains("Inline[Inline[\"A\"] = 1] = \"A\";", output);
    }

    [Fact]
    public void TypeScriptParserShouldPreservePrivateClassFields()
    {
        var input = """
            class Service {
                #value = 1;
                read() {
                    return this.#value;
                }
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("#value = 1;", output);
        Assert.Contains("return this.#value;", output);
    }

    [Fact]
    public void TypeScriptParserShouldErasePrivateFieldTypeMarkers()
    {
        var input = """
            class Service {
                #value!: number;
                static #total?: number;
                read(): number {
                    return this.#value;
                }
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("#value;", output);
        Assert.Contains("static #total;", output);
        Assert.Contains("return this.#value;", output);
        Assert.DoesNotContain("!", output);
        Assert.DoesNotContain("?:", output);
        Assert.DoesNotContain(": number", output);
    }

    [Fact]
    public void TypeScriptParserShouldPreservePrivateClassMethods()
    {
        var input = """
            class Service {
                #read() {
                    return 1;
                }
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("#read() {", output);
        Assert.Contains("return 1;", output);
    }

    [Fact]
    public void TypeScriptParserShouldLowerModuleNamespaceDeclarationsLikeTypeScript60()
    {
        var input = File.ReadAllText("Input/TypeScript/UnsupportedNamespaces/module.ts");

        var output = PrintTypeScript(input);

        Assert.Contains("var Runtime;", output);
        Assert.Contains("Runtime.value = 1;", output);
        Assert.Contains("})(Runtime || (Runtime = {}));", output);
    }

    [Fact]
    public void TypeScriptParserShouldSkipFunctionTypeAnnotations()
    {
        var input = """
            export var beforeRenderCallback: (node: Node, phase: Phase) => void = noop;
            export function setBeforeRender(callback: (node: Node, phase: Phase) => void): (node: Node, phase: Phase) => void {
                return callback;
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("export var beforeRenderCallback = noop;", output);
        Assert.Contains("export function setBeforeRender(callback) {", output);
    }

    [Fact]
    public void TypeScriptParserShouldEraseExportedTypeDeclarations()
    {
        var input = """
            export type Message = string | number;
            export interface DelayedMessage {
                message: Message;
            }
            export default interface DefaultMessage {
                message: string;
            }
            export default class RuntimeDefault {
                message = "runtime";
            }
            export const value: Message = "ok";
            """;

        var output = PrintTypeScript(input);

        Assert.DoesNotContain("Message", output);
        Assert.DoesNotContain("interface", output);
        Assert.Contains("export default class RuntimeDefault", output);
        Assert.Contains("export const value = \"ok\";", output);
    }

    [Fact]
    public void TypeScriptParserShouldEraseTypeOnlyExportStars()
    {
        var input = """
            export type * from "./types";
            export type * as Types from "./more-types";
            export const value = 1;
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("export const value = 1;", output);
        Assert.DoesNotContain("Types", output);
        Assert.DoesNotContain("more-types", output);
        Assert.DoesNotContain("export type", output);
    }

    [Fact]
    public void TypeScriptParserShouldPreserveModuleMarkerForErasedTypeOnlyDeclarationsLikeTypeScript60()
    {
        var input = """
            import type Foo from "foo";
            export type Bar = string;
            export interface Shape {
                value: string;
            }
            const value = 1;
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(actualJs));
        Assert.Contains("const value = 1;", actualJs);
        Assert.Contains("export { };", actualJs);
        Assert.DoesNotContain("Foo", actualJs);
        Assert.DoesNotContain("Shape", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldPreserveValueSpecifiersNamedType()
    {
        var input = """
            import { type as importedType, type ValueOnly } from "./mod";
            export { type as exportedType, type ExportOnly } from "./mod";
            console.log(importedType);
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("import { type as importedType } from \"./mod\";", output);
        Assert.Contains("export { type as exportedType } from \"./mod\";", output);
        Assert.Contains("console.log(importedType);", output);
        Assert.DoesNotContain("ValueOnly", output);
        Assert.DoesNotContain("ExportOnly", output);
    }

    [Fact]
    public void TypeScriptParserShouldSkipTypePredicatesAndThisParameters()
    {
        var input = """
            export function isObject(val: any): val is { [name: string]: any } {
                return typeof val === "object";
            }
            export const DndCtx = function (this: IDndCtx, pointerId: number) {
                this.id = pointerId;
            };
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("export function isObject(val) {", output);
        Assert.Contains("export const DndCtx = function(pointerId) {", output);
    }

    [Fact]
    public void TypeScriptParserShouldSkipImportTypesInAnnotations()
    {
        var input = """
            type Mod = import("./mod").Mod;
            const mod: typeof import("./mod") = load();
            function use(value: import("./mod").Value): void {
                console.log(value);
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("const mod = load();", output);
        Assert.Contains("function use(value) {", output);
        Assert.Contains("console.log(value);", output);
        Assert.DoesNotContain("import(\"./mod\")", output);
        Assert.DoesNotContain("Mod", output);
    }

    [Fact]
    public void TypeScriptParserShouldEraseInterfaceCallSignaturesWithNestedObjectTypes()
    {
        var input = """
            export interface CVA {
                close: "}";
                templated: `}`;
                <T>(
                    config: T extends Shape
                        ? Base & {
                              variants?: T;
                              compound?: (T extends Shape ? ({ [K in keyof T]?: T[K] } & Extra) : Extra)[];
                          }
                        : Base,
                ): (props?: T extends Shape ? Props<T> & Extra : Extra) => Styles;
            }
            export const value = 1;
            """;

        var output = PrintTypeScript(input);

        Assert.DoesNotContain("interface", output);
        Assert.Contains("export const value = 1;", output);
    }

    [Fact]
    public void TypeScriptParserShouldSkipObjectConstructorTypeAssertions()
    {
        var input = """
            export const DndCtx = function (this: IDndCtx, pointerId: number) {
                this.id = pointerId;
            } as unknown as { new (pointerId: number): IDndStartCtx & IDndOverCtx };
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("export const DndCtx = function(pointerId) {", output);
        Assert.DoesNotContain("unknown", output);
    }

    [Fact]
    public void TypeScriptParserShouldNotTreatTernaryQuestionAsOptionalParameter()
    {
        var input = """
            export function normalize(value: boolean) {
                return value ? "true" : "false";
            }
            export function choose(value: boolean, first: () => void, second: () => void) {
                return value ? first : second;
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("return value ? \"true\" : \"false\";", output);
        Assert.Contains("return value ? first : second;", output);
    }

    [Fact]
    public void TypeScriptParserShouldParseNestedTypedArrowExpressionBodies()
    {
        var input = """
            export const factory = ((name: string) => (params?: Object) => params)(name);
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("export const factory = (name => params => params)(name);", output);
    }

    [Fact]
    public void TypeScriptParserShouldSkipAngleBracketTypeAssertions()
    {
        var input = """
            export function getValue(comp: Builder) {
                if ("x" in <any>comp) return <(params?: Object) => string>comp.build();
                switch (comp.kind) {
                    default: {
                        break;
                    }
                }
                return <(params?: Object, hashArg?: string) => string>comp.build();
                return undefined;
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("\"x\" in comp", output);
        Assert.Contains("return comp.build();", output);
    }

    [Fact]
    public void TypeScriptParserShouldSkipGenericClassMethodParameters()
    {
        var input = """
            class MediaRuleBuilder {
                tokens: TokenType[] = [];
                pushOptionalTokens<T extends RuleBehaviourType>(behaviour?: T) {
                    return behaviour;
                }
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("pushOptionalTokens(behaviour) {", output);
    }

    [Fact]
    public void TypeScriptParserShouldSkipConstTypeParameters()
    {
        var input = """
            function id<const T extends readonly string[]>(value: T): T {
                return value;
            }
            const arrow = <const T extends string>(value: T): T => value;
            class Box<const T> {
                value!: T;
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("function id(value) {", output);
        Assert.Contains("const arrow = value => value;", output);
        Assert.Contains("class Box {", output);
        Assert.Contains("value;", output);
        Assert.DoesNotContain("const T", output);
    }

    [Fact]
    public void TypeScriptParserShouldSkipNestedImplementsHeritageTypes()
    {
        var input = """
            class Service extends Base<string, Map<number, string>>
                implements Repository<{ value: string }>, Disposable {
                value = 1;
            }
            class Nested extends ns.Base<string>.Nested<number> implements Other<string> {}
            class Mixed extends mixin<string>(Base).with<number>(Other) implements MixedType {}
            class Asserted extends (Base as Constructor) implements AssertedType {}
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("class Service extends Base {", output);
        Assert.Contains("class Nested extends ns.Base.Nested", output);
        Assert.Contains("class Mixed extends(mixin(Base).with(Other)){}", output);
        Assert.Contains("class Asserted extends Base", output);
        Assert.Contains("value = 1;", output);
        Assert.DoesNotContain("implements", output);
        Assert.DoesNotContain("Repository", output);
        Assert.DoesNotContain("Disposable", output);
        Assert.DoesNotContain("Other<string>", output);
        Assert.DoesNotContain("Constructor", output);
    }

    [Fact]
    public void TypeScriptParserShouldEraseInterfacesWithMappedAndIndexTypes()
    {
        var input = """
            export type Unsafe<T = any> = { -readonly [P in keyof T]: T[P] };
            export declare class Tagged<N extends string> {
                private "nominal type hack"?: N;
            }
            export interface Context<TData = any> {
                cfg: any | undefined;
                refs: { [name: string]: Node | undefined } | undefined;
            }
            export const value = 1;
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("export const value = 1;", output);
        Assert.DoesNotContain("refs", output);
        Assert.DoesNotContain("Tagged", output);
    }

    [Fact]
    public void TypeScriptParserShouldSkipLeadingPipeInTypeAnnotation()
    {
        var input = """
            var x: { a: number; } = { a: 1 };
            var y:
                | { b: number; }
                | undefined;
            export const z = 1;
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("export const z = 1;", output);
    }

    [Fact]
    public void TypeScriptParserShouldHandleForLoopBeforeMultiLineArrowIIFE()
    {
        var input = """
            function f(x: any): any { return x; }
            export function compile(msgAst: any): any {
                if (true) {
                    return (params?: any, hashArg?: any) => {
                        for (let i = 0; i < 1; i++) { }
                        return "y";
                    };
                }
                switch (msgAst.type) {
                    case "arg":
                        return (
                            (name: any) => (params?: any) => name
                        )(msgAst.id);
                    default: return "z";
                }
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("function compile(msgAst)", output);
        Assert.Contains("name)(msgAst.id)", output);
    }

    [Fact]
    public void TypeScriptParserShouldLowerDottedNamespaces()
    {
        var input = """
            namespace Outer.Inner {
                export const value: number = 1;
                export function read(): number {
                    return value;
                }
            }
            class Merged {}
            namespace Merged.Inner {
                export const value = 2;
            }
            var VarOuter;
            namespace VarOuter.Inner {
                export const value = 3;
            }
            console.log(Outer.Inner.value, Outer.Inner.read());
            console.log(Merged.Inner.value, VarOuter.Inner.value);
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("var Outer;", output);
        Assert.Contains("var Inner;", output);
        Assert.Contains("})(Inner = Outer.Inner || (Outer.Inner = {}));", output);
        Assert.Contains("Inner.value = 1;", output);
        Assert.Contains("Inner.read = read;", output);
        Assert.Contains("class Merged", output);
        Assert.Contains("})(Merged || (Merged = {}));", output);
        Assert.Contains("Inner.value = 2;", output);
        Assert.Equal(0, CountOccurrences(output, "var Merged;"));
        Assert.Equal(2, CountOccurrences(output, "var VarOuter;"));
        Assert.Contains("Inner.value = 3;", output);
        Assert.Contains("console.log(Outer.Inner.value, Outer.Inner.read());", output);
        Assert.Contains("console.log(Merged.Inner.value, VarOuter.Inner.value);", output);
    }

    [Fact]
    public void TypeScriptParserShouldLowerExportedDottedNamespaces()
    {
        var input = """
            export namespace Outer.Inner {
                export const value = 1;
                export function read(): number {
                    return value;
                }
            }
            console.log(Outer.Inner.value);
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("export var Outer;", output);
        Assert.Contains("var Inner;", output);
        Assert.Contains("Inner.value = 1;", output);
        Assert.Contains("Inner.read = read;", output);
        Assert.Contains("})(Inner = Outer.Inner || (Outer.Inner = {}));", output);
        Assert.Contains("console.log(Outer.Inner.value);", output);
    }

    [Fact]
    public void TypeScriptParserShouldEraseTypeOnlyDottedNamespacesLikeTypeScript60()
    {
        var input = """
            namespace Outer.Inner {
                export type T = string;
            }
            const value = 1;
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(actualJs));
        Assert.Equal("const value = 1;", actualJs.TrimEnd());
    }

    [Fact]
    public void TypeScriptParserShouldPreserveRuntimeDottedNamespaceMergeAfterTypeOnlyNamespaceLikeTypeScript60()
    {
        var input = """
            namespace Outer.Inner {
                export type T = string;
            }
            namespace Outer.Inner {
                export const value = 1;
            }
            console.log(Outer.Inner.value);
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(actualJs));
        Assert.Contains("Inner.value = 1;", actualJs);
        Assert.Contains("console.log(Outer.Inner.value);", actualJs);
        Assert.DoesNotContain("(function(Inner) {})(Inner = Outer.Inner", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldMarkExportedTypeOnlyNamespacesAsModulesLikeTypeScript60()
    {
        var input = """
            export namespace Runtime {
                export type T = string;
            }
            const value = 1;
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(actualJs));
        Assert.Contains("const value = 1;", actualJs);
        Assert.Contains("export { };", actualJs);
        Assert.DoesNotContain("Runtime", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldLowerNestedExportedNamespaces()
    {
        var input = """
            namespace Outer {
                export namespace Inner {
                    export const value = 1;
                }
            }
            console.log(Outer.Inner.value);
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("var Outer;", output);
        Assert.Contains("let Inner;", output);
        Assert.Contains("Inner.value = 1;", output);
        Assert.Contains("})(Inner = Outer.Inner || (Outer.Inner = {}));", output);
        Assert.Contains("console.log(Outer.Inner.value);", output);
    }

    [Fact]
    public void TypeScriptParserShouldLowerNamespaceClassAndFunctionExports()
    {
        var input = """
            namespace Runtime {
                export class Service {
                    static create() {
                        return new Service();
                    }
                }
                export function make() {
                    return Service.create();
                }
            }
            console.log(Runtime.make());
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("class Service {", output);
        Assert.Contains("return new Service();", output);
        Assert.Contains("Runtime.Service = Service;", output);
        Assert.Contains("function make() {", output);
        Assert.Contains("return Service.create();", output);
        Assert.Contains("Runtime.make = make;", output);
        Assert.Contains("console.log(Runtime.make());", output);
    }

    [Fact]
    public void TypeScriptParserShouldMergeNamespacesWithRuntimeDeclarationsLikeTypeScript60()
    {
        var input = """
            function Fn() {}
            namespace Fn {
                export const value = 1;
            }
            class Cls {}
            namespace Cls {
                export const value = 2;
            }
            enum Enm {
                A = 1
            }
            namespace Enm {
                export const value = 3;
            }
            namespace Plain {
                export const first = 4;
            }
            namespace Plain {
                export const second = 5;
            }
            function outer() {
                function Local() {}
                namespace Local {
                    export const value = 6;
                }
                return Local.value;
            }
            export function Exported() {}
            export namespace Exported {
                export const value = 7;
            }
            var VarMerge;
            namespace VarMerge {
                export const value = 8;
            }
            let LetMerge;
            namespace LetMerge {
                export const value = 9;
            }
            const ConstMerge = {};
            namespace ConstMerge {
                export const value = 10;
            }
            console.log(Fn.value, Cls.value, Enm.A, Enm.value, Plain.first, Plain.second, outer(), Exported.value,
                VarMerge.value, LetMerge.value, ConstMerge.value);
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("function Fn()", output);
        Assert.Contains("class Cls", output);
        Assert.Contains("var Enm;", output);
        Assert.Contains("var Plain;", output);
        Assert.Contains("function Local()", output);
        Assert.Contains("export function Exported()", output);
        Assert.Contains("Fn.value = 1;", output);
        Assert.Contains("Cls.value = 2;", output);
        Assert.Contains("Enm.value = 3;", output);
        Assert.Contains("Plain.first = 4;", output);
        Assert.Contains("Plain.second = 5;", output);
        Assert.Contains("Local.value = 6;", output);
        Assert.Contains("Exported.value = 7;", output);
        Assert.Contains("VarMerge.value = 8;", output);
        Assert.Contains("LetMerge.value = 9;", output);
        Assert.Contains("ConstMerge.value = 10;", output);
        Assert.Equal(0, CountOccurrences(output, "var Fn;"));
        Assert.Equal(0, CountOccurrences(output, "var Cls;"));
        Assert.Equal(1, CountOccurrences(output, "var Enm;"));
        Assert.Equal(1, CountOccurrences(output, "var Plain;"));
        Assert.Equal(0, CountOccurrences(output, "let Local;"));
        Assert.Equal(0, CountOccurrences(output, "export var Exported;"));
        Assert.Equal(2, CountOccurrences(output, "var VarMerge;"));
        Assert.Equal(1, CountOccurrences(output, "let LetMerge;"));
        Assert.Equal(1, CountOccurrences(output, "var LetMerge;"));
        Assert.Equal(1, CountOccurrences(output, "const ConstMerge = {};"));
        Assert.Equal(1, CountOccurrences(output, "var ConstMerge;"));
    }

    [Fact]
    public void TypeScriptParserShouldLowerLocalNamespacesLikeTypeScript60()
    {
        var input = """
            class C {
                static {
                    namespace N {
                        export const x = 1;
                    }
                    console.log(N.x);
                }
            }
            function f() {
                namespace Local {
                    export const value = 2;
                }
                return Local.value;
            }
            if (ok)
                namespace Direct {
                    export const value = 3;
                }
            if (ok)
                const enum DirectConstEnum {
                    Value = 11
                }
            label:
                namespace Labeled {
                    export const value = 7;
                }
            enumLabel:
                enum LabeledEnum {
                    Value = 8
                }
            constEnumLabel:
                const enum LabeledConstEnum {
                    Value = 12
                }
            switch (kind) {
                case 1:
                    namespace InCase {
                        export const value = 4;
                    }
                    enum InCaseEnum {
                        Value = 9
                    }
                    const enum InCaseConstEnum {
                        Value = 10
                    }
                    console.log(InCase.value);
                    console.log(InCaseEnum.Value);
                    console.log(InCaseConstEnum.Value);
                    break;
            }
            function g() {
                module ModuleAlias {
                    export const value = 5;
                }
                namespace Dotted.Inner {
                    export const value = 6;
                }
                return ModuleAlias.value + Dotted.Inner.value;
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("static {\n        let N;", output);
        Assert.Contains("N.x = 1;", output);
        Assert.Contains("console.log(N.x);", output);
        Assert.Contains("let Local;", output);
        Assert.Contains("return Local.value;", output);
        Assert.Contains("if (ok) {\n    var Direct;", output);
        Assert.Contains("Direct.value = 3;", output);
        Assert.Contains("if (ok) {\n    var DirectConstEnum;", output);
        Assert.Contains("DirectConstEnum[DirectConstEnum[\"Value\"] = 11] = \"Value\";", output);
        Assert.Contains("label: {\n    var Labeled;", output);
        Assert.Contains("Labeled.value = 7;", output);
        Assert.Contains("enumLabel: {\n    var LabeledEnum;", output);
        Assert.Contains("LabeledEnum[LabeledEnum[\"Value\"] = 8] = \"Value\";", output);
        Assert.Contains("constEnumLabel: {\n    var LabeledConstEnum;", output);
        Assert.Contains("LabeledConstEnum[LabeledConstEnum[\"Value\"] = 12] = \"Value\";", output);
        Assert.Contains("case 1:\n    let InCase;", output);
        Assert.Contains("InCase.value = 4;", output);
        Assert.Contains("console.log(InCase.value);", output);
        Assert.DoesNotContain("case 1:\n    {\n        let InCaseEnum;", output);
        Assert.Contains("let InCaseEnum;", output);
        Assert.Contains("InCaseEnum[InCaseEnum[\"Value\"] = 9] = \"Value\";", output);
        Assert.Contains("console.log(InCaseEnum.Value);", output);
        Assert.Contains("let InCaseConstEnum;", output);
        Assert.Contains("InCaseConstEnum[InCaseConstEnum[\"Value\"] = 10] = \"Value\";", output);
        Assert.Contains("console.log(InCaseConstEnum.Value);", output);
        Assert.Contains("let ModuleAlias;", output);
        Assert.Contains("ModuleAlias.value = 5;", output);
        Assert.Contains("let Dotted;", output);
        Assert.Contains("let Inner;", output);
        Assert.Contains("Inner.value = 6;", output);
        Assert.Contains("return ModuleAlias.value + Dotted.Inner.value;", output);
    }

    [Fact]
    public void TypeScriptParserShouldRewriteNamespaceExportedVariableReferences()
    {
        var input = """
            namespace Runtime {
                export let uninitialized;
                export const value = 1;
                export const doubled = value * 2;
                export let count = 0;
                count++;
                export function read() {
                    return value;
                }
                export function shadow(value: number) {
                    return value;
                }
                export class Service {
                    static read() {
                        return value;
                    }
                }
                try {
                    throw 2;
                } catch (value) {
                    console.log(value);
                }
                {
                    let value = 2;
                    console.log(value);
                }
                for (let value of [value]) {
                    console.log(value);
                }
                for (let i = 0; i < value; i++) {
                    let value = i;
                    console.log(value);
                }
                switch (value) {
                    case value:
                        let value = 3;
                        console.log(value);
                        break;
                }
                console.log(value);
            }
            """;

        var output = PrintTypeScript(input);

        Assert.DoesNotContain("Runtime.uninitialized = void 0;", output);
        Assert.Contains("Runtime.value = 1;", output);
        Assert.Contains("Runtime.doubled = Runtime.value * 2;", output);
        Assert.Contains("Runtime.count = 0;", output);
        Assert.Contains("Runtime.count++;", output);
        Assert.Contains("return Runtime.value;", output);
        Assert.Contains("return value;", output);
        Assert.Contains("return Runtime.value;", output);
        Assert.Contains("catch (value)", output);
        Assert.Contains("console.log(value);", output);
        Assert.Contains("let value = 2;", output);
        Assert.Contains("for (let value of [ Runtime.value ])", output);
        Assert.Contains("for (let i = 0; i < Runtime.value; i++)", output);
        Assert.Contains("switch (Runtime.value)", output);
        Assert.Contains("case value:", output);
        Assert.Contains("let value = 3;", output);
        Assert.Contains("console.log(Runtime.value);", output);
    }

    [Fact]
    public void TypeScriptParserShouldPreserveNamespaceExportedEnumLocalReferences()
    {
        var input = """
            namespace Runtime {
                export enum Mode {
                    A = 1
                }
                export const value = Mode.A;
                export function read() {
                    return Mode.A;
                }
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("let Mode;", output);
        Assert.Contains("})(Mode = Runtime.Mode || (Runtime.Mode = {}));", output);
        Assert.Contains("Runtime.value = Mode.A;", output);
        Assert.Contains("return Mode.A;", output);
    }

    [Fact]
    public void TypeScriptParserShouldLowerNamespaceConstEnumsLikeTypeScript60()
    {
        var input = """
            namespace Runtime {
                const enum LocalMode {
                    A = 1
                }
                export const enum ExportedMode {
                    B = 2
                }
                export const value = LocalMode.A + ExportedMode.B;
            }
            console.log(Runtime.value);
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("let LocalMode;", output);
        Assert.Contains("})(LocalMode || (LocalMode = {}));", output);
        Assert.Contains("let ExportedMode;", output);
        Assert.Contains("})(ExportedMode = Runtime.ExportedMode || (Runtime.ExportedMode = {}));", output);
        Assert.Contains("Runtime.value = LocalMode.A + ExportedMode.B;", output);
        Assert.DoesNotContain("Runtime.value = 3;", output);
    }

    [Fact]
    public void TypeScriptParserShouldLowerNamespaceExportedDestructuring()
    {
        var input = """
            namespace Runtime {
                export const { x, nested: { y, ...innerRest }, ["z"]: z, [dynamicKey]: dynamic, maybe = fallback, ...rest } = source;
                export const [first, , third = fallback, ...tail] = values;
                console.log(x, y, innerRest, z, dynamic, maybe, rest, first, third, tail);
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("Runtime.x = source.x", output);
        Assert.Contains("_a = source.nested", output);
        Assert.Contains("Runtime.y = _a.y", output);
        Assert.Contains("Runtime.innerRest = __rest(_a, [ \"y\" ])", output);
        Assert.Contains("Runtime.z = source[\"z\"]", output);
        Assert.Contains("_b = dynamicKey", output);
        Assert.Contains("Runtime.dynamic = source[_b]", output);
        Assert.Contains("Runtime.maybe = _c === void 0 ? fallback : _c", output);
        Assert.Contains("Runtime.rest = __rest(source, [ \"x\", \"nested\", \"z\", typeof _b === \"symbol\" ? _b : _b + \"\", \"maybe\" ])", output);
        Assert.Contains("Runtime.first = values[0]", output);
        Assert.Contains("Runtime.third = _d === void 0 ? fallback : _d", output);
        Assert.Contains("Runtime.tail = values.slice(3)", output);
        Assert.Contains("console.log(Runtime.x, Runtime.y, Runtime.innerRest, Runtime.z, Runtime.dynamic, Runtime.maybe, Runtime.rest, Runtime.first, Runtime.third, Runtime.tail);", output);
    }

    [Fact]
    public void TypeScriptParserShouldEraseNamespaceExportSpecifierLists()
    {
        var input = """
            namespace Runtime {
                const local = 1;
                export { local as exposed };
            }
            console.log(Runtime.exposed);
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("const local = 1;", output);
        Assert.Contains("console.log(Runtime.exposed);", output);
        Assert.DoesNotContain("Runtime.exposed =", output);
        Assert.DoesNotContain("export {", output);
    }

    [Fact]
    public void TypeScriptParserShouldEraseTypeOnlyAndDeclareNamespaceMembers()
    {
        var input = """
            namespace N {
                export type T = string;
                export interface I {
                    x: T;
                }
                export declare const ambient: string;
                declare namespace Nested {
                    export const x: string;
                }
                export const value = 1;
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("var N;", output);
        Assert.Contains("N.value = 1;", output);
        Assert.DoesNotContain("interface", output);
        Assert.DoesNotContain("ambient", output);
        Assert.DoesNotContain("Nested", output);
    }

    [Fact]
    public void TypeScriptParserShouldEraseTypeOnlyTypeNamedImportExportSpecifiers()
    {
        var input = """
            import { type type as ImportedType } from "types";
            export { type type as ExportedType } from "types";
            const value = 1;
            """;

        var output = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(output));
        Assert.Contains("const value = 1;", output);
        Assert.Contains("export { };", output);
    }

    [Fact]
    public void TypeScriptParserShouldLowerNamespaceDefaultClassExportByName()
    {
        var input = """
            namespace Runtime {
                export default class Service {}
            }
            console.log(Runtime.Service);
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("class Service {}", output);
        Assert.Contains("Runtime.Service = Service;", output);
        Assert.Contains("console.log(Runtime.Service);", output);
        Assert.DoesNotContain("export default", output);
    }

    [Fact]
    public void TypeScriptParserShouldEmitConstructorParameterDecoratorsAsClassDecorators()
    {
        var input = """
            function param(target: unknown, key: string | undefined, index: number) {}

            class Service {
                constructor(@param public ...value: string[]) {}
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("let Service = class Service {", output);
        Assert.Contains("constructor(...value) {", output);
        Assert.Contains("this.value = value;", output);
        Assert.Contains("Service = __decorate([ __param(0, param) ], Service);", output);
        Assert.DoesNotContain("Service.prototype, \"constructor\"", output);
    }

    [Fact]
    public void TypeScriptParserShouldEmitLocalConstructorParameterDecoratorsLikeTypeScript60()
    {
        var input = """
            function param(target: unknown, key: string | undefined, index: number) {}

            function make() {
                class Local {
                    constructor(@param value: string) {}
                }
                return Local;
            }

            {
                class BlockLocal {
                    constructor(@param value: string) {}
                }
                console.log(BlockLocal);
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("let Local = class Local {", output);
        Assert.Contains("Local = __decorate([ __param(0, param) ], Local);", output);
        Assert.Contains("return Local;", output);
        Assert.Contains("let BlockLocal = class BlockLocal {", output);
        Assert.Contains("BlockLocal = __decorate([ __param(0, param) ], BlockLocal);", output);
        Assert.Contains("console.log(BlockLocal);", output);
    }

    [Fact]
    public void TypeScriptParserShouldMergeComputedMethodAndParameterDecoratorsLikeTypeScript60()
    {
        var input = """
            function field(target: unknown, key: string) {}
            function dec(target: unknown, key: string | undefined, index: number) {}
            function key() { return "x"; }

            class Service {
                @field [key()](@dec value: string) {}
                @field static [key()](@dec value: string) {}
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("var _a, _b;", output);
        Assert.Contains("[_a = key()](value) {}", output);
        Assert.Contains("static [_b = key()](value) {}", output);
        Assert.Contains("__decorate([ field, __param(0, dec) ], Service.prototype, _a, null);", output);
        Assert.Contains("__decorate([ field, __param(0, dec) ], Service, _b, null);", output);
        Assert.DoesNotContain("Service.prototype, \"_a\"", output);
        Assert.DoesNotContain("__decorate([ __param(0, dec) ], Service.prototype", output);
    }

    [Fact]
    public void TypeScriptParserShouldLowerComputedAccessorAndParameterDecoratorsLikeTypeScript60()
    {
        var input = """
            function field(target: unknown, key: string) {}
            function dec(target: unknown, key: string | undefined, index: number) {}
            function key() { return "x"; }

            class Service {
                @field get [key()]() { return 1; }
                @field set [key()](@dec value: number) {}
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("var _a, _b;", output);
        Assert.Contains("get [_a = key()]()", output);
        Assert.Contains("set [_b = key()](value)", output);
        Assert.Contains("__decorate([ field ], Service.prototype, _a, null);", output);
        Assert.Contains("__decorate([ field, __param(0, dec) ], Service.prototype, _b, null);", output);
    }

    [Fact]
    public void TypeScriptParserShouldExportDecoratedNamespaceClassesLikeTypeScript60()
    {
        var input = """
            function param(target: unknown, key: string | undefined, index: number) {}

            namespace Runtime {
                export class Service {
                    constructor(@param value: string) {}
                }
                export const selected = Service;
            }
            console.log(Runtime.selected);
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("let Service = class Service {", output);
        Assert.Contains("Service = __decorate([ __param(0, param) ], Service);", output);
        Assert.Contains("Runtime.Service = Service;", output);
        Assert.Contains("Runtime.selected = Service;", output);
        Assert.Contains("console.log(Runtime.selected);", output);
    }

    [Fact]
    public void TypeScriptParserShouldExportNamespaceClassesAfterMemberDecoratorsLikeTypeScript60()
    {
        var input = """
            function field(target: unknown, key: string) {}

            namespace Runtime {
                export class Service {
                    @field value = 1;
                    @field static ready = true;
                    @field [key()] = 2;
                    @field static [key()] = 3;
                    @field [methodKey()]() {}
                }
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("class Service {", output);
        Assert.Contains("__decorate([ field ], Service.prototype, \"value\", void 0);", output);
        Assert.Contains("__decorate([ field ], Service, \"ready\", void 0);", output);
        Assert.Contains("var _a, _b, _c;", output);
        Assert.Contains("[_a = key()] = 2;", output);
        Assert.Contains("static [_b = key()] = 3;", output);
        Assert.Contains("[_c = methodKey()]() {}", output);
        Assert.Contains("__decorate([ field ], Service.prototype, _a, void 0);", output);
        Assert.Contains("__decorate([ field ], Service, _b, void 0);", output);
        Assert.Contains("__decorate([ field ], Service.prototype, _c, null);", output);
        Assert.Contains("Runtime.Service = Service;", output);
        Assert.True(output.IndexOf("__decorate([ field ], Service.prototype, \"value\", void 0);", StringComparison.Ordinal) <
                    output.IndexOf("Runtime.Service = Service;", StringComparison.Ordinal));
        Assert.True(output.IndexOf("__decorate([ field ], Service, \"ready\", void 0);", StringComparison.Ordinal) <
                    output.IndexOf("Runtime.Service = Service;", StringComparison.Ordinal));
        Assert.True(output.IndexOf("__decorate([ field ], Service.prototype, _c, null);", StringComparison.Ordinal) <
                    output.IndexOf("__decorate([ field ], Service, \"ready\", void 0);", StringComparison.Ordinal));
        Assert.True(output.IndexOf("__decorate([ field ], Service, _b, void 0);", StringComparison.Ordinal) <
                    output.IndexOf("Runtime.Service = Service;", StringComparison.Ordinal));
    }

    [Fact]
    public void TypeScriptParserShouldExportDefaultDecoratedNamespaceClassesLikeTypeScript60()
    {
        var input = """
            function sealed(target: unknown) {}
            function param(target: unknown, key: string | undefined, index: number) {}

            namespace Runtime {
                @sealed
                export default class Service {
                    constructor(@param value: string) {}
                }
                export const selected = Service;
            }
            console.log(Runtime.Service, Runtime.selected);
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("let Service = class Service {", output);
        Assert.Contains("Service = __decorate([ sealed, __param(0, param) ], Service);", output);
        Assert.Contains("Runtime.Service = Service;", output);
        Assert.Contains("Runtime.selected = Service;", output);
        Assert.Contains("console.log(Runtime.Service, Runtime.selected);", output);
        Assert.DoesNotContain("export default", output);
    }

    [Fact]
    public void TypeScriptParserShouldCombineClassAndConstructorParameterDecorators()
    {
        var input = """
            function sealed(target: unknown) {}
            function param(target: unknown, key: string | undefined, index: number) {}

            @sealed
            class Service {
                constructor(@param value: string) {}
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("Service = __decorate([ sealed, __param(0, param) ], Service);", output);
        Assert.DoesNotContain("Service = __decorate([ __param(0, param) ], Service);", output);
    }

    [Fact]
    public void TypeScriptParserShouldDecorateExportedClassesBeforeExporting()
    {
        var input = """
            function sealed(target: unknown) {}

            @sealed
            export class Service {}
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("let Service = class Service {};", output);
        Assert.Contains("Service = __decorate([ sealed ], Service);", output);
        Assert.Contains("export { Service };", output);
    }

    [Fact]
    public void TypeScriptParserShouldDecorateClassesWithDecoratorAfterExportLikeTypeScript60()
    {
        var input = """
            function sealed(target: unknown) {}

            export @sealed class Service {}
            export @sealed abstract class AbstractService {}
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("let Service = class Service {};", output);
        Assert.Contains("Service = __decorate([ sealed ], Service);", output);
        Assert.Contains("export { Service };", output);
        Assert.Contains("let AbstractService = class AbstractService {};", output);
        Assert.Contains("AbstractService = __decorate([ sealed ], AbstractService);", output);
        Assert.Contains("export { AbstractService };", output);
    }

    [Fact]
    public void TypeScriptParserShouldDecorateDefaultExportedClassesBeforeExporting()
    {
        var input = """
            function sealed(target: unknown) {}

            @sealed
            export default class Service {}
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("let Service = class Service {};", output);
        Assert.Contains("Service = __decorate([ sealed ], Service);", output);
        Assert.Contains("export default Service;", output);
    }

    [Fact]
    public void TypeScriptParserShouldDecorateDefaultClassesWithDecoratorAfterExportLikeTypeScript60()
    {
        var input = """
            function sealed(target: unknown) {}

            export default @sealed class Service {}
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("let Service = class Service {};", output);
        Assert.Contains("Service = __decorate([ sealed ], Service);", output);
        Assert.Contains("export default Service;", output);
    }

    [Fact]
    public void TypeScriptParserShouldDecorateAnonymousDefaultClassesWithDecoratorAfterExportLikeTypeScript60()
    {
        var input = """
            function sealed(target: unknown) {}

            export default @sealed class {}
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("let default_1 = class {};", output);
        Assert.Contains("default_1 = __decorate([ sealed ], default_1);", output);
        Assert.Contains("export default default_1;", output);
    }

    [Fact]
    public void TypeScriptParserShouldDecorateMembersAndParametersOnAnonymousDefaultClassesLikeTypeScript60()
    {
        var input = """
            function sealed(target: unknown) {}
            function field(target: unknown, key: string) {}
            function param(target: unknown, key: string | undefined, index: number) {}

            export default @sealed class {
                @field value = 1;
                constructor(@param value: string) {}
                method(@param value: string) {}
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("let default_1 = class {", output);
        Assert.Contains("__decorate([ field ], default_1.prototype, \"value\", void 0);", output);
        Assert.Contains("__decorate([ __param(0, param) ], default_1.prototype, \"method\", null);", output);
        Assert.Contains("default_1 = __decorate([ sealed, __param(0, param) ], default_1);", output);
        Assert.Contains("export default default_1;", output);
    }

    [Fact]
    public void TypeScriptParserShouldDecorateDefaultClassesWithDecoratorBetweenExportAndDefaultLikeTypeScript60()
    {
        var input = """
            function sealed(target: unknown) {}

            export @sealed default class Service {}
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("let Service = class Service {};", output);
        Assert.Contains("Service = __decorate([ sealed ], Service);", output);
        Assert.Contains("export default Service;", output);
    }

    [Fact]
    public void TypeScriptParserShouldDecorateAnonymousDefaultClassesWithDecoratorBetweenExportAndDefaultLikeTypeScript60()
    {
        var input = """
            function sealed(target: unknown) {}

            export @sealed default class {}
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("let default_1 = class {};", output);
        Assert.Contains("default_1 = __decorate([ sealed ], default_1);", output);
        Assert.Contains("export default default_1;", output);
    }

    [Fact]
    public void TypeScriptParserShouldDecorateAnonymousDefaultAbstractClassesWithDecoratorBetweenExportAndDefaultLikeTypeScript60()
    {
        var input = """
            function sealed(target: unknown) {}

            export @sealed default abstract class {}
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("let default_1 = class {};", output);
        Assert.Contains("default_1 = __decorate([ sealed ], default_1);", output);
        Assert.Contains("export default default_1;", output);
    }

    [Fact]
    public void TypeScriptParserShouldDecorateDefaultAbstractClassesWithDecoratorBetweenExportAndDefaultLikeTypeScript60()
    {
        var input = """
            function sealed(target: unknown) {}

            export @sealed default abstract class AbstractService {}
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("let AbstractService = class AbstractService {};", output);
        Assert.Contains("AbstractService = __decorate([ sealed ], AbstractService);", output);
        Assert.Contains("export default AbstractService;", output);
    }

    [Fact]
    public void TypeScriptParserShouldDecorateDefaultAbstractClassesWithDecoratorAfterExportLikeTypeScript60()
    {
        var input = """
            function sealed(target: unknown) {}

            export default @sealed abstract class AbstractService {}
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("let AbstractService = class AbstractService {};", output);
        Assert.Contains("AbstractService = __decorate([ sealed ], AbstractService);", output);
        Assert.Contains("export default AbstractService;", output);
    }

    [Fact]
    public void TypeScriptParserShouldDecorateAbstractFieldsLikeTypeScript60()
    {
        var input = """
            function field(target: unknown, key: string) {}

            abstract class Service {
                @field abstract value: string;
                @field abstract accessor ready: boolean;
                @field abstract [key]: string;
                @field static abstract staticValue: string;
                @field readonly abstract readonlyValue: string;
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("class Service {}", output);
        Assert.Contains("__decorate([ field ], Service.prototype, \"value\", void 0);", output);
        Assert.Contains("__decorate([ field ], Service.prototype, \"ready\", void 0);", output);
        Assert.Contains("__decorate([ field ], Service.prototype, key, void 0);", output);
        Assert.Contains("__decorate([ field ], Service.prototype, \"staticValue\", void 0);", output);
        Assert.Contains("__decorate([ field ], Service.prototype, \"readonlyValue\", void 0);", output);
        Assert.DoesNotContain("#ready_accessor_storage", output);
    }

    [Fact]
    public void TypeScriptParserShouldPreserveDecoratedStaticFieldInitializers()
    {
        var input = """
            function field(target: unknown, key: string) {}

            class Service {
                @field static value: number = 1;
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("static value = 1;", output);
        Assert.Contains("__decorate([ field ], Service, \"value\", void 0);", output);
    }

    [Fact]
    public void TypeScriptParserShouldPreserveDecoratedInstanceFieldInitializers()
    {
        var input = """
            function field(target: unknown, key: string) {}

            class Service {
                @field value: number = 1;
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("value = 1;", output);
        Assert.Contains("__decorate([ field ], Service.prototype, \"value\", void 0);", output);
    }

    [Fact]
    public void TypeScriptParserShouldDecorateDeclaredFieldsLikeTypeScript60()
    {
        var input = """
            function field(target: unknown, key: string) {}

            class Service {
                @field declare value: string;
                @field declare [key]: string;
                @field static declare staticValue: string;
                @field declare #privateValue: string;
                @field declare accessor ready: boolean;
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("class Service {}", output);
        Assert.Contains("__decorate([ field ], Service.prototype, \"value\", void 0);", output);
        Assert.Contains("__decorate([ field ], Service.prototype, key, void 0);", output);
        Assert.Contains("__decorate([ field ], Service.prototype, \"staticValue\", void 0);", output);
        Assert.Contains("__decorate([ field ], Service.prototype, \"ready\", void 0);", output);
        Assert.DoesNotContain("declare", output);
        Assert.DoesNotContain("value: string", output);
        Assert.DoesNotContain("privateValue", output);
    }

    [Fact]
    public void TypeScriptParserShouldInsertDecoratedDerivedFieldInitializersAfterSuper()
    {
        var input = """
            function field(target: unknown, key: string) {}

            class Base {}
            class Service extends Base {
                @field value: number = 1;
                constructor() {
                    super();
                    this.ready = true;
                }
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("value = 1;", output);
        Assert.Contains("""
            constructor() {
                    super();
                    this.ready = true;
                }
            """, output);
        Assert.Contains("__decorate([ field ], Service.prototype, \"value\", void 0);", output);
    }

    [Fact]
    public void TypeScriptParserShouldCreateDerivedConstructorsForDecoratedFieldInitializers()
    {
        var input = """
            function field(target: unknown, key: string) {}

            class Base {}
            class Service extends Base {
                @field value: number = 1;
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("value = 1;", output);
        Assert.DoesNotContain("constructor(...args)", output);
        Assert.Contains("__decorate([ field ], Service.prototype, \"value\", void 0);", output);
    }

    [Fact]
    public void TypeScriptParserShouldOrderParameterPropertiesBeforeDecoratedFieldInitializers()
    {
        var input = """
            function field(target: unknown, key: string) {}

            class Service {
                @field value: number = 1;
                constructor(public name: string, readonly id = 1, public ...tags: string[]) {}
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("""
            constructor(name, id = 1, ...tags) {
                    this.name = name;
                    this.id = id;
                    this.tags = tags;
                }
            """, output);
        Assert.Contains("value = 1;", output);
    }

    [Fact]
    public void TypeScriptParserShouldEmitParameterPropertyFieldsBeforeOtherClassElementsLikeTypeScript60()
    {
        var input = """
            class Base {}
            class Service extends Base {
                static {
                    use(Service);
                }
                field = read(this.name);
                constructor(public name: string) {
                    super();
                }
            }
            use(Service);
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(actualJs));
        Assert.True(actualJs.IndexOf("name;", StringComparison.Ordinal) <
                    actualJs.IndexOf("static {", StringComparison.Ordinal));
        Assert.True(actualJs.IndexOf("name;", StringComparison.Ordinal) <
                    actualJs.IndexOf("field = read(this.name);", StringComparison.Ordinal));
    }

    [Fact]
    public void TypeScriptParserShouldTreatStringConstructorMethodAsMethodLikeTypeScript60()
    {
        var input = """
            class Service {
                "constructor"() {
                    use("method");
                }
                constructor(public name: string) {}
            }
            use(Service);
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Contains("constructor() {", oracleJs);
        Assert.Contains("\"constructor\"()", actualJs);
        Assert.Contains("constructor(name)", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldTreatPrivateConstructorMethodAsMethodLikeTypeScript60()
    {
        var input = """
            class Service {
                #constructor() {
                    use("method");
                }
                constructor(public name: string) {
                    this.#constructor();
                }
            }
            use(Service);
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(actualJs));
        Assert.Contains("#constructor()", actualJs);
        Assert.Contains("constructor(name)", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldOrderDerivedParameterPropertiesBeforeDecoratedFieldInitializers()
    {
        var input = """
            function field(target: unknown, key: string) {}

            class Base {}
            class Service extends Base {
                @field value: number = 1;
                constructor(public override name: string) {
                    super();
                }
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("""
            constructor(name) {
                    super();
                    this.name = name;
                }
            """, output);
        Assert.Contains("value = 1;", output);
    }

    [Fact]
    public void TypeScriptParserShouldPreserveModifierFieldInitializers()
    {
        var input = """
            class Service {
                public value: number = 1;
                private hidden = 2;
                readonly name = "x";
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("value = 1;", output);
        Assert.Contains("hidden = 2;", output);
        Assert.Contains("name = \"x\";", output);
    }

    [Fact]
    public void TypeScriptParserShouldPreservePlainClassFieldInitializers()
    {
        var input = """
            class Base {
                value = 1;
                empty: number;
                static cache = createCache();
                static declared: string;
            }
            class Derived extends Base {
                derived = 2;
                constructor() {
                    super();
                    this.ready = true;
                }
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("value = 1;", output);
        Assert.Contains("empty;", output);
        Assert.Contains("static cache = createCache();", output);
        Assert.Contains("static declared;", output);
        Assert.Contains("derived = 2;", output);
        Assert.Contains("super();", output);
    }

    [Fact]
    public void TypeScriptParserShouldEraseOverrideClassMemberModifiers()
    {
        var input = """
            class Derived extends Base {
                override value = 1;
                override run(value: string): string {
                    return value;
                }
                override get ready(): boolean {
                    return true;
                }
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("value = 1;", output);
        Assert.Contains("run(value) {", output);
        Assert.Contains("get ready() {", output);
        Assert.DoesNotContain("override", output);
    }

    [Fact]
    public void TypeScriptParserShouldPreserveStaticBlocksAfterStaticFieldsInOrder()
    {
        var input = """
            class Service {
                static first = log("first");
                static {
                    log("block");
                }
                static second = log("second");
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("static first = log(\"first\");", output);
        Assert.Contains("static {", output);
        Assert.Contains("log(\"block\");", output);
        Assert.Contains("static second = log(\"second\");", output);
        Assert.True(output.IndexOf("static first = log(\"first\");", System.StringComparison.Ordinal) <
                    output.IndexOf("log(\"block\");", System.StringComparison.Ordinal));
        Assert.True(output.IndexOf("log(\"block\");", System.StringComparison.Ordinal) <
                    output.IndexOf("static second = log(\"second\");", System.StringComparison.Ordinal));
    }

    [Fact]
    public void TypeScriptParserShouldEraseStaticBlockExportTypesWithoutModuleMarkerLikeTypeScript60()
    {
        var input = """
            class Service {
                static {
                    export type Local = string;
                    use(1);
                }
            }
            use(Service);
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(actualJs));
        Assert.DoesNotContain("export {", actualJs);
        Assert.Contains("use(1);", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldLowerUsingDeclarationsInIndependentStaticBlockScopes()
    {
        var input = """
            class SyncResources {
                static {
                    using resource = acquire();
                    resource.use();
                }
            }
            class AsyncResources {
                static {
                    await using resource = acquireAsync();
                    resource.use();
                }
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("class SyncResources", output);
        Assert.Contains("const resource = __addDisposableResource(env_1, acquire(), false);", output);
        Assert.Contains("__disposeResources(env_1);", output);
        Assert.Contains("class AsyncResources", output);
        Assert.Contains("const resource = __addDisposableResource(env_2, acquireAsync(), true);", output);
        Assert.Contains("const result_1 = __disposeResources(env_2);", output);
        Assert.Contains("if (result_1) await result_1;", output);
    }

    [Fact]
    public void TypeScriptParserShouldDecorateLocalClassesInsideStaticBlocksLikeTypeScript60()
    {
        var input = """
            function dec(target: unknown) {}

            class Host {
                static {
                    @dec
                    class Local {
                        static {
                            using resource = acquire();
                            resource.use();
                        }
                    }
                    console.log(Local);
                }
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("let Local = class Local {", output);
        Assert.Contains("Local = __decorate([ dec ], Local);", output);
        Assert.Contains("const resource = __addDisposableResource(env_1, acquire(), false);", output);
        Assert.Contains("console.log(Local);", output);
    }

    [Fact]
    public void TypeScriptParserShouldDecorateLocalClassesInStatementBodiesLikeTypeScript60()
    {
        var input = """
            const ok = true;
            function dec(target: unknown) {}
            function param(target: unknown, key: string | undefined, index: number) {}

            if (ok)
                @dec
                class IfLocal {
                    constructor(@param value: number) {}
                }

            label:
                @dec
                class LabelLocal {}

            switch (mode) {
                case 1:
                    @dec
                    class SwitchLocal {}
                    break;
            }
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs, removeTypeScriptHelpers: true),
            DumpJavaScriptAstWithoutPositions(actualJs, removeTypeScriptHelpers: true));
    }

    [Fact]
    public void TypeScriptParserShouldPreserveStaticModifierFieldInitializers()
    {
        var input = """
            class Service {
                public static value: number = 1;
                private static hidden = 2;
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("static value = 1;", output);
        Assert.Contains("static hidden = 2;", output);
    }

    [Fact]
    public void TypeScriptParserShouldOrderModifierFieldInitializersAfterParameterProperties()
    {
        var input = """
            class Base {}
            class Service extends Base {
                public value = 1;
                constructor(public name: string) {
                    super();
                    this.ready = true;
                }
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("""
            constructor(name) {
                    super();
                    this.name = name;
                    this.ready = true;
                }
            """, output);
        Assert.Contains("value = 1;", output);
    }

    [Fact]
    public void TypeScriptParserShouldPreserveComputedModifierFieldInitializers()
    {
        var input = """
            const key = "value";
            class Service {
                public [key]: number = 1;
                public static [key]: number = 2;
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("[key] = 1;", output);
        Assert.Contains("static [key] = 2;", output);
    }

    [Fact]
    public void TypeScriptParserShouldPreserveComputedDecoratedFieldInitializers()
    {
        var input = """
            function field(target: unknown, key: string) {}
            const key = "value";
            class Service {
                @field
                [key]: number = 1;
                @field
                static [key]: number = 2;
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("var _a, _b;", output);
        Assert.Contains("[_a = key] = 1;", output);
        Assert.Contains("static [_b = key] = 2;", output);
        Assert.Contains("__decorate([ field ], Service.prototype, _a, void 0);", output);
        Assert.Contains("__decorate([ field ], Service, _b, void 0);", output);
    }

    [Fact]
    public void TypeScriptParserShouldEraseDeclareClassFields()
    {
        var input = """
            class Service {
                declare value: string;
                declare static cache: Map<string, string>;
                declare readonly name?: string;
                declare runLater(value: string): void;
                run() {
                    return 1;
                }
            }
            """;

        var output = PrintTypeScript(input);

        Assert.DoesNotContain("value", output);
        Assert.DoesNotContain("cache", output);
        Assert.DoesNotContain("name", output);
        Assert.DoesNotContain("runLater", output);
        Assert.Contains("run() {", output);
    }

    [Fact]
    public void TypeScriptParserShouldEraseAbstractClassModifiersAndMembers()
    {
        var input = """
            export default abstract class Base {
                abstract run(): void;
                value = 1;
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("export default class Base {", output);
        Assert.Contains("value = 1;", output);
        Assert.DoesNotContain("abstract", output);
        Assert.DoesNotContain("run", output);
    }

    [Fact]
    public void TypeScriptParserShouldEraseAbstractAndDeclareMembersWithObjectTypes()
    {
        var input = """
            abstract class Base {
                abstract run(): { value: string };
                declare metadata: { ready: boolean };
                value = 1;
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("class Base {", output);
        Assert.Contains("value = 1;", output);
        Assert.DoesNotContain("run", output);
        Assert.DoesNotContain("metadata", output);
        Assert.DoesNotContain("ready", output);
    }

    [Fact]
    public void TypeScriptParserShouldEraseClassMethodOverloadSignatures()
    {
        var input = """
            class Service {
                run(value: string): string;
                run(value: number): number;
                read(value: ")"): string;
                run(value: string | number): string | number {
                    return value;
                }
                read(value: string): string {
                    return value;
                }
            }
            """;

        var output = PrintTypeScript(input);

        Assert.DoesNotContain("run(value) {}", output);
        Assert.DoesNotContain("read(value) {}", output);
        Assert.Contains("run(value) {", output);
        Assert.Contains("read(value) {", output);
        Assert.Contains("return value;", output);
    }

    [Fact]
    public void TypeScriptParserShouldEraseStaticClassMethodOverloadSignatures()
    {
        var input = """
            class Service {
                static create(value: string): Service;
                static create(value: number): Service;
                static create(value: string | number): Service {
                    return new Service();
                }
            }
            """;

        var output = PrintTypeScript(input);

        Assert.DoesNotContain("create(value) {}", output);
        Assert.Contains("static create(value) {", output);
        Assert.Contains("return new Service();", output);
    }

    [Fact]
    public void TypeScriptParserShouldMatchAccessorOverloadEmit()
    {
        var input = """
            class Service {
                get value(): string;
                get value(): string {
                    return "ready";
                }
                static get cached(): string;
                static get cached(): string {
                    return "cached";
                }
                set next(value: string);
                set next(value: string) {}
                set tricky(value: ")");
                set tricky(value: string) {}
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("get value() {}", output);
        Assert.Contains("get value() {", output);
        Assert.Contains("return \"ready\";", output);
        Assert.Contains("static get cached() {}", output);
        Assert.Contains("return \"cached\";", output);
        Assert.Contains("set next(value) {}", output);
        Assert.Contains("set tricky(value) {}", output);
    }

    [Fact]
    public void TypeScriptParserShouldMatchDeclareAccessorEmit()
    {
        var input = """
            class Service {
                declare get value(): string;
                declare get "x-y"(): string;
                declare get [dynamicName](): string;
                declare get #secret(): string;
                declare static set cached(value: string);
                declare field: string;
                ready = true;
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("get value() {}", output);
        Assert.Contains("get \"x-y\"() {}", output);
        Assert.Contains("get [dynamicName]() {}", output);
        Assert.Contains("get #secret() {}", output);
        Assert.Contains("static set cached(value) {}", output);
        Assert.Contains("ready = true;", output);
        Assert.DoesNotContain("field", output);
        Assert.DoesNotContain("declare", output);
        Assert.DoesNotContain(": string", output);
    }

    [Fact]
    public void TypeScriptParserShouldEraseConstructorOverloadSignatures()
    {
        var input = """
            class Service {
                constructor(value: string);
                constructor(value: ")");
                constructor(value: number);
                constructor(value: string | number) {
                    this.value = value;
                }
            }
            """;

        var output = PrintTypeScript(input);

        Assert.DoesNotContain("constructor(value) {}", output);
        Assert.Contains("constructor(value) {", output);
        Assert.Contains("this.value = value;", output);
    }

    [Fact]
    public void TypeScriptParserShouldEraseFunctionOverloadSignaturesWithLiteralDelimiters()
    {
        var input = """
            function read(value: ")"): string;
            function read(value: "]"): string;
            function readEquals(value: string): "=";
            function read(value: string): string {
                return value;
            }
            function readEquals(value: string): string {
                return value;
            }
            use(read);
            use(readEquals);
            """;

        var output = PrintTypeScript(input);

        Assert.DoesNotContain("read(value) {}", output);
        Assert.Contains("function read(value) {", output);
        Assert.DoesNotContain("readEquals(value) {}", output);
        Assert.Contains("function readEquals(value) {", output);
        Assert.Contains("return value;", output);
    }

    [Fact]
    public void TypeScriptParserShouldSkipOverloadedObjectTypeAnnotations()
    {
        var input = """
            const fn: {
                (value: string): string;
                (value: number): number;
                tag?: string;
            } = value => value;
            console.log(fn(1));
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("const fn = value => value;", output);
        Assert.Contains("console.log(fn(1));", output);
    }

    [Fact]
    public void TypeScriptParserShouldSkipConstructorObjectTypeAnnotations()
    {
        var input = """
            const Ctor: {
                new (value: string): { value: string };
                prototype: object;
            } = class {
                constructor(value: string) {}
            };
            console.log(new Ctor("x"));
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("const Ctor = class {", output);
        Assert.Contains("constructor(value) {}", output);
        Assert.Contains("console.log(new Ctor(\"x\"));", output);
    }

    [Fact]
    public void TypeScriptParserShouldEraseImportEqualsDeclarations()
    {
        var input = """
            import fs = require("fs");
            import type Foo = require("foo");
            const value = 1;
            """;

        var output = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(output));
        Assert.DoesNotContain("require", output);
        Assert.Contains("const value = 1;", output);
        Assert.Contains("export { };", output);
    }

    [Fact]
    public void TypeScriptParserShouldEraseTypeOnlyNamespaceAndDefaultImports()
    {
        var input = """
            import type * as Types from "./types";
            import type Foo, { Bar as Baz } from "./more-types";
            import { runtime } from "./runtime";
            console.log(runtime);
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("import { runtime } from \"./runtime\";", output);
        Assert.Contains("console.log(runtime);", output);
        Assert.DoesNotContain("Types", output);
        Assert.DoesNotContain("Foo", output);
        Assert.DoesNotContain("Baz", output);
        Assert.DoesNotContain("more-types", output);
    }

    [Fact]
    public void TypeScriptParserShouldPreserveArbitraryModuleSpecifierNamesLikeTypeScript60()
    {
        var input = """
            import { type "type-name" as TypeName, "runtime-name" as runtimeName } from "./runtime";
            export { localName as "arbitrary-name" };
            export { "source-name" as renamedName } from "./runtime";
            const localName = 1;
            console.log(runtimeName);
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("import { \"runtime-name\" as runtimeName } from \"./runtime\";", output);
        Assert.Contains("export { localName as \"arbitrary-name\" };", output);
        Assert.Contains("export { \"source-name\" as renamedName } from \"./runtime\";", output);
        Assert.Contains("const localName = 1;", output);
        Assert.Contains("console.log(runtimeName);", output);
        Assert.DoesNotContain("type-name", output);
        Assert.DoesNotContain("TypeName", output);
    }

    [Fact]
    public void TypeScriptParserShouldPreserveImportDeferDeclarations()
    {
        var input = """
            import defer featureDefault from "./feature-default";
            import defer * as feature from "./feature";
            import defer { value } from "./values";
            console.log(featureDefault, feature, value);
            """;

        var output = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(output));
        Assert.Contains("import defer featureDefault from \"./feature-default\";", output);
        Assert.Contains("import defer * as feature from \"./feature\";", output);
        Assert.Contains("import defer { value } from \"./values\";", output);
        Assert.Contains("console.log(featureDefault, feature, value);", output);
    }

    [Fact]
    public void TypeScriptParserShouldPreserveImportAndExportAssertAttributes()
    {
        var input = """
            import data from "./data.json" assert { type: "json" };
            import type typeOnlyData from "./type-only.json" with { type: "json" };
            import type TypeDefault from "./type-default.json" with { type: "json" };
            import { type Shape, value } from "./runtime" with { mode: "live" };
            export { default as other } from "./other.json" assert { type: "json" };
            export type { Shape } from "./runtime" with { mode: "live" };
            export type * from "./type-star" with { mode: "type" };
            export type * as TypeNamespace from "./type-namespace" with { mode: "type" };
            export type { default as TypeDefaultExport } from "./type-default-export" with { mode: "type" };
            export { value } from "./runtime" with { mode: "live" };
            console.log(data);
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("import data from \"./data.json\" assert {", output);
        Assert.Contains("import { value } from \"./runtime\" with {", output);
        Assert.Contains("export { default as other } from \"./other.json\" assert {", output);
        Assert.Contains("export { value } from \"./runtime\" with {", output);
        Assert.Contains("type: \"json\"", output);
        Assert.Contains("mode: \"live\"", output);
        Assert.Contains("console.log(data);", output);
        Assert.DoesNotContain("typeOnlyData", output);
        Assert.DoesNotContain("TypeDefault", output);
        Assert.DoesNotContain("Shape", output);
        Assert.DoesNotContain("./type-only.json", output);
        Assert.DoesNotContain("./type-star", output);
        Assert.DoesNotContain("TypeNamespace", output);
        Assert.DoesNotContain("TypeDefaultExport", output);
    }

    [Fact]
    public void TypeScriptParserShouldPreserveRuntimeImportEqualsAliases()
    {
        var input = """
            namespace Runtime {
                export const value = 1;
            }
            import Alias = Runtime.value;
            console.log(Alias);
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("var Alias = Runtime.value;", output);
        Assert.Contains("console.log(Alias);", output);
    }

    [Fact]
    public void TypeScriptParserShouldEraseTypeOnlyNamespacesAndAliasesLikeTypeScript60()
    {
        var input = """
            namespace Runtime {
                export type T = string;
                export interface Shape {
                    value: string;
                }
            }
            import Alias = Runtime.T;
            const value = 1;
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(actualJs));
        Assert.Equal("const value = 1;", actualJs.TrimEnd());
    }

    [Fact]
    public void TypeScriptParserShouldPreserveAliasesAfterRuntimeNamespaceMergeLikeTypeScript60()
    {
        var input = """
            namespace Runtime {
                export type T = string;
            }
            namespace Runtime {
                export const value = 1;
            }
            import Alias = Runtime.value;
            console.log(Alias);
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(actualJs));
        Assert.Contains("var Alias = Runtime.value;", actualJs);
        Assert.Contains("console.log(Alias);", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldEraseTypeOnlyExportImportAliasesLikeTypeScript60()
    {
        var input = """
            namespace Runtime {
                export type T = string;
                export import Alias = Runtime.T;
            }
            namespace Mixed {
                export type T = string;
                export const value = 1;
                export import Alias = Mixed.T;
            }
            const value = 1;
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(actualJs));
        Assert.Contains("(function(Runtime) {})", actualJs);
        Assert.Contains("Mixed.value = 1;", actualJs);
        Assert.DoesNotContain("Runtime.Alias", actualJs);
        Assert.DoesNotContain("Mixed.Alias", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldPreserveNamespaceShellsForErasedExportImportAliasesLikeTypeScript60()
    {
        var input = """
            namespace Runtime {
                export import Fs = require("fs");
            }
            namespace Outer.Inner {
                export type T = string;
                export import Alias = Outer.Inner.T;
            }
            const value = 1;
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(actualJs));
        Assert.Contains("(function(Runtime) {})", actualJs);
        Assert.Contains("(function(Inner) {})", actualJs);
        Assert.DoesNotContain("Runtime.Fs", actualJs);
        Assert.DoesNotContain("Inner.Alias", actualJs);
        Assert.DoesNotContain("require", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldPreserveNamespaceShellsForErasedExportStarsLikeTypeScript60()
    {
        var input = """
            namespace Runtime {
                export * from "mod";
            }
            namespace More {
                export * as ns from "mod";
            }
            namespace Types {
                export type * from "types";
            }
            const value = 1;
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(actualJs));
        Assert.Contains("(function(Runtime) {})", actualJs);
        Assert.Contains("(function(More) {})", actualJs);
        Assert.Contains("(function(Types) {})", actualJs);
        Assert.DoesNotContain("from \"mod\"", actualJs);
        Assert.DoesNotContain("from \"types\"", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldIgnoreNamespaceShellTriggersInCommentsLikeTypeScript60()
    {
        var input = """
            namespace Runtime {
                // export * from "mod";
                /* export import Alias = Runtime.T; */
                export type T = "export * from ignored";
            }
            const value = 1;
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(actualJs));
        Assert.Equal("const value = 1;", actualJs.TrimEnd());
    }

    [Fact]
    public void TypeScriptParserShouldPreserveNamespaceShellsForErasedValueExportSpecifiersLikeTypeScript60()
    {
        var input = """
            namespace Runtime {
                export { missing };
            }
            namespace NamedTypeValue {
                const type = 1;
                export { type as value };
            }
            namespace TypesOnly {
                type T = string;
                export type { T };
            }
            const value = 1;
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Contains("(function (Runtime)", oracleJs);
        Assert.Contains("(function (NamedTypeValue)", oracleJs);
        Assert.DoesNotContain("var TypesOnly", oracleJs);
        Assert.Contains("(function(Runtime) {})", actualJs);
        Assert.Contains("const type = 1;", actualJs);
        Assert.DoesNotContain("TypesOnly", actualJs);
        Assert.DoesNotContain("export { missing", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldEraseNamespaceSpecifiersForLocalTypeOnlyDeclarationsLikeTypeScript60()
    {
        var input = """
            namespace TypesOnly {
                type T = string;
                interface I {
                    value: string;
                }
                export { T, I as PublicI };
            }
            use(TypesOnly);
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(actualJs));
        Assert.DoesNotContain("var TypesOnly", oracleJs);
        Assert.DoesNotContain("var TypesOnly", actualJs);
        Assert.Equal("use(TypesOnly);", actualJs.TrimEnd());
    }

    [Fact]
    public void TypeScriptParserShouldPreserveExportedRuntimeImportEqualsAliases()
    {
        var input = """
            namespace Runtime {
                export const value = 1;
                export import Alias = Runtime.value;
            }
            export import ExportedAlias = Runtime.value;
            console.log(Runtime.Alias, ExportedAlias);
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("Runtime.Alias = Runtime.value;", output);
        Assert.Contains("export var ExportedAlias = Runtime.value;", output);
        Assert.Contains("console.log(Runtime.Alias, ExportedAlias);", output);
    }

    [Fact]
    public void TypeScriptParserShouldLowerNestedNamespaceImportEqualsAliases()
    {
        var input = """
            namespace Runtime.Inner {
                export const value = 1;
                export import Alias = Runtime.Inner.value;
            }
            namespace Local {
                const value = 2;
                import Alias = value;
                console.log(Alias);
            }
            console.log(Runtime.Inner.Alias);
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("Inner.value = 1;", output);
        Assert.Contains("Inner.Alias = Runtime.Inner.value;", output);
        Assert.Contains("var Alias = value;", output);
        Assert.Contains("console.log(Alias);", output);
        Assert.Contains("console.log(Runtime.Inner.Alias);", output);
    }

    [Fact]
    public void TypeScriptParserShouldEraseExportEqualsDeclarations()
    {
        var input = """
            const value = 1;
            export = value;
            """;

        var output = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(output));
        Assert.DoesNotContain("export =", output);
        Assert.Contains("const value = 1;", output);
        Assert.Contains("export { };", output);
    }

    [Fact]
    public void TypeScriptParserShouldEraseExportAsNamespaceDeclarations()
    {
        var input = """
            export as namespace MyLibrary;
            export const value = 1;
            """;

        var output = PrintTypeScript(input);

        Assert.DoesNotContain("namespace MyLibrary", output);
        Assert.Contains("export const value = 1;", output);
    }

    [Fact]
    public void TypeScriptParserShouldEraseNamespaceExportAsNamespaceDeclarationsLikeTypeScript60()
    {
        var input = """
            namespace Runtime {
                export as namespace RuntimeGlobal;
                export const value = 1;
            }
            console.log(Runtime.value);
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(actualJs));
        Assert.DoesNotContain("RuntimeGlobal", actualJs);
        Assert.Contains("Runtime.value = 1;", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldEraseAmbientDeclareBlocks()
    {
        var input = """
            declare enum Mode {
                A,
                B
            }
            declare namespace JSX {
                interface IntrinsicElements {
                    div: any;
                }
            }
            declare module "./core" {
                export interface Shape {
                    value: string;
                }
            }
            declare module "./literals" {
                export const closeBrace: "}";
                export const template: `}`;
                export const pattern: /}/;
            }
            const value = 1;
            """;

        var output = PrintTypeScript(input);

        Assert.DoesNotContain("Mode", output);
        Assert.DoesNotContain("namespace", output);
        Assert.DoesNotContain("module", output);
        Assert.Contains("const value = 1;", output);
    }

    [Fact]
    public void TypeScriptParserShouldPreserveEmptyExportsLikeTypeScript60()
    {
        var input = """
            declare global {
                interface Window {
                    value: string;
                }
            }
            export {};
            export {} from "./side-effect-module";
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Contains("export {", oracleJs);
        Assert.Contains("export { };", actualJs);
        Assert.Contains("export { } from \"./side-effect-module\";", actualJs);
        Assert.DoesNotContain("declare global", actualJs);
        Assert.DoesNotContain("interface Window", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldEraseAmbientDeclareStatements()
    {
        var input = """
            declare const DEBUG: boolean;
            declare function external(value: string): number;
            declare let state: string;
            declare export class ExternalClass {
                value: string;
            }
            declare abstract class AbstractExternalClass {
                abstract value: string;
            }
            declare export abstract class ExportedAbstractExternalClass {
                abstract value: string;
            }
            declare export default abstract class DefaultAbstractExternalClass {
                abstract value: string;
            }
            const value = 1;
            """;

        var output = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(output));
        Assert.DoesNotContain("DEBUG", output);
        Assert.DoesNotContain("external", output);
        Assert.DoesNotContain("state", output);
        Assert.DoesNotContain("ExternalClass", output);
        Assert.DoesNotContain("AbstractExternalClass", output);
        Assert.DoesNotContain("ExportedAbstractExternalClass", output);
        Assert.DoesNotContain("DefaultAbstractExternalClass", output);
        Assert.Contains("const value = 1;", output);
        Assert.Contains("export { };", output);
    }

    [Fact]
    public void TypeScriptParserShouldEraseAmbientConstEnums()
    {
        var input = """
            declare const enum Ambient {
                A = 1
            }
            const value = 1;
            """;

        var output = PrintTypeScript(input);

        Assert.DoesNotContain("Ambient", output);
        Assert.Contains("const value = 1;", output);
    }

    [Fact]
    public void TypeScriptParserShouldEraseExportedAmbientDeclarations()
    {
        var input = """
            export declare const DEBUG: boolean;
            export declare function external(value: string): number;
            export declare enum RuntimeMode {
                A = 1
            }
            export declare const enum AmbientMode {
                B = 2
            }
            export declare namespace RuntimeNamespace {
                export const value: string;
            }
            export declare class RuntimeClass {
                get value(): string;
            }
            export declare abstract class RuntimeAbstractClass {
                abstract value: string;
            }
            export const value = 1;
            """;

        var output = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(output));
        Assert.DoesNotContain("DEBUG", output);
        Assert.DoesNotContain("external", output);
        Assert.DoesNotContain("RuntimeMode", output);
        Assert.DoesNotContain("AmbientMode", output);
        Assert.DoesNotContain("RuntimeNamespace", output);
        Assert.DoesNotContain("RuntimeClass", output);
        Assert.DoesNotContain("RuntimeAbstractClass", output);
        Assert.Contains("export const value = 1;", output);
    }

    [Fact]
    public void TypeScriptParserShouldSkipInstantiationExpressionTypeArguments()
    {
        var input = """
            function id<T>(value: T): T {
                return value;
            }
            const fn = id<string>;
            const prop = namespaceObject.id<string>.prop;
            const asserted = id<string> as (value: string) => string;
            const checked = id<string> satisfies (value: string) => string;
            const assertedMember = namespaceObject.id<string> as (value: string) => string;
            const checkedMember = namespaceObject.id<string> satisfies (value: string) => string;
            const nestedCtor = new namespaceObject.Ctor<string>.Nested();
            console.log(fn("x"));
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("const fn = id;", output);
        Assert.Contains("const prop = namespaceObject.id.prop;", output);
        Assert.Contains("const asserted = id;", output);
        Assert.Contains("const checked = id;", output);
        Assert.Contains("const assertedMember = namespaceObject.id;", output);
        Assert.Contains("const checkedMember = namespaceObject.id;", output);
        Assert.Contains("const nestedCtor = new namespaceObject.Ctor.Nested();", output);
        Assert.Contains("console.log(fn(\"x\"));", output);
    }

    [Fact]
    public void TypeScriptParserShouldSkipTypeArgumentsBeforeOptionalCallsAndTaggedTemplates()
    {
        var input = """
            const optional = obj.fn<string>?.(value);
            const tagged = tag<string>`hello ${name}`;
            const optionalTagged = tag?.<string>`optional ${name}`;
            const optionalMemberTagged = obj?.method<string>`member ${name}`;
            const parenthesizedOptionalMemberTagged = (obj?.method)<string>`parenthesized ${name}`;
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("const optional = obj.fn?.(value);", output);
        Assert.Contains("const tagged = tag`hello ${name}`;", output);
        Assert.Contains("const optionalTagged = tag`optional ${name}`;", output);
        Assert.Contains("const optionalMemberTagged = (obj?.method)`member ${name}`;", output);
        Assert.Contains("const parenthesizedOptionalMemberTagged = (obj?.method)`parenthesized ${name}`;", output);
        Assert.DoesNotContain("<string>", output);
    }

    [Fact]
    public void TypeScriptParserShouldSkipTypeArgumentsOnImportAndSuperCalls()
    {
        var input = """
            const loaded = import<typeof module>("./module", { with: { type: "json" } });
            const loadedProp = import<typeof module>("./module").prop;
            class Derived extends Base {
                constructor() {
                    super<string>(loaded);
                    super<string>.prop();
                }
                read() {
                    const prop = super.read<string>.prop;
                    return super.read<string>(loaded);
                }
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("const loaded = import(\"./module\", {", output);
        Assert.Contains("const loadedProp = import(\"./module\").prop;", output);
        Assert.Contains("super(loaded);", output);
        Assert.Contains("super.prop();", output);
        Assert.Contains("const prop = super.read.prop;", output);
        Assert.Contains("return super.read(loaded);", output);
        Assert.DoesNotContain("<typeof module>", output);
        Assert.DoesNotContain("<string>", output);
    }

    [Fact]
    public void TypeScriptParserShouldKeepSpacedRelationalExpressions()
    {
        var input = """
            const value = a < b > c;
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("const value = a < b > c;", output);
    }

    [Fact]
    public void TypeScriptParserShouldEvaluateConstEnumShiftAndUnaryMemberValues()
    {
        var input = """
            const enum Flags {
                None = 0,
                Read = 1 << 0,
                Write = 1 << 1,
                All = Read | Write,
                Neg = -1
            }
            console.log(Flags.Read, Flags.Write, Flags.All, Flags.Neg);
            """;

        var output = PrintTypeScript(input);

        Assert.DoesNotContain("var Flags", output);
        Assert.Contains("console.log(1, 2, 3, -1);", output);
    }

    [Fact]
    public void TypeScriptParserShouldEvaluateRuntimeEnumConstantsLikeTypeScript()
    {
        var input = """
            enum Mode {
                A = 1,
                B = A + 2,
                C,
                Template = `value`,
                AfterTemplate,
                1 = "one",
                StringValue = "text",
                StringAlias = StringValue,
                ["computed-name"] = 5,
                AfterComputed,
                "a\"b" = 7,
                ["escaped\\name"] = 8,
                Nan = NaN,
                Inf = Infinity,
                NegInf = -Infinity,
                AfterNonFinite,
                StringBeforeImplicit = "text",
                AfterString,
                TrueValue = true as any,
                NullValue = null as any,
                UndefinedValue = undefined as any,
                constructor = 9,
                __proto__ = 10
            }
            console.log(Mode.B, Mode.C, Mode.Template, Mode[0]);
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("Mode[Mode[\"B\"] = 3] = \"B\";", output);
        Assert.Contains("Mode[Mode[\"C\"] = 4] = \"C\";", output);
        Assert.Contains("Mode[\"Template\"] = \"value\";", output);
        Assert.Contains("Mode[Mode[\"AfterTemplate\"] = void 0] = \"AfterTemplate\";", output);
        Assert.Contains("Mode[1] = \"one\";", output);
        Assert.Contains("Mode[\"StringAlias\"] = \"text\";", output);
        Assert.Contains("Mode[Mode[\"computed-name\"] = 5] = \"computed-name\";", output);
        Assert.Contains("Mode[Mode[\"AfterComputed\"] = 6] = \"AfterComputed\";", output);
        Assert.Contains("a\"b", output);
        Assert.Contains("escaped\\\\name", output);
        Assert.Contains("Mode[Mode[\"Nan\"] = NaN] = \"Nan\";", output);
        Assert.Contains("Mode[Mode[\"Inf\"] = Infinity] = \"Inf\";", output);
        Assert.Contains("Mode[Mode[\"NegInf\"] = -Infinity] = \"NegInf\";", output);
        Assert.Contains("Mode[Mode[\"AfterNonFinite\"] = void 0] = \"AfterNonFinite\";", output);
        Assert.Contains("Mode[\"StringBeforeImplicit\"] = \"text\";", output);
        Assert.Contains("Mode[Mode[\"AfterString\"] = void 0] = \"AfterString\";", output);
        Assert.Contains("Mode[Mode[\"TrueValue\"] = true] = \"TrueValue\";", output);
        Assert.Contains("Mode[Mode[\"NullValue\"] = null] = \"NullValue\";", output);
        Assert.Contains("Mode[Mode[\"UndefinedValue\"] = undefined] = \"UndefinedValue\";", output);
        Assert.Contains("Mode[Mode[\"constructor\"] = 9] = \"constructor\";", output);
        Assert.Contains("Mode[Mode[\"__proto__\"] = 10] = \"__proto__\";", output);
        Assert.DoesNotContain("A + 2", output);
        Assert.DoesNotContain("`value`", output);
    }

    [Fact]
    public void TypeScriptParserShouldReverseMapRuntimeEnumStringExpressionInitializers()
    {
        var input = """
            enum Runtime {
                Length = "a".length,
                Upper = "b".toUpperCase()
            }
            console.log(Runtime.Length, Runtime.Upper);
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("Runtime[Runtime[\"Length\"] = \"a\".length] = \"Length\";", output);
        Assert.Contains("Runtime[Runtime[\"Upper\"] = \"b\".toUpperCase()] = \"Upper\";", output);
    }

    [Fact]
    public void TypeScriptParserShouldFoldRuntimeEnumStringConstantExpressions()
    {
        var input = """
            enum Runtime {
                Joined = "a" + "b",
                WithNumber = "a" + 1,
                Alias = Joined
            }
            console.log(Runtime.Joined, Runtime.WithNumber, Runtime.Alias);
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("Runtime[\"Joined\"] = \"ab\";", output);
        Assert.Contains("Runtime[\"WithNumber\"] = \"a1\";", output);
        Assert.Contains("Runtime[\"Alias\"] = \"ab\";", output);
        Assert.DoesNotContain("Runtime[Runtime[\"Joined\"]", output);
    }

    [Fact]
    public void TypeScriptParserShouldPreserveRuntimeEnumDynamicStringAliasesLikeTypeScript60()
    {
        var input = """
            enum Runtime {
                Template = `${foo}`,
                TemplateAlias = Template,
                TemplateJoined = Template + 1,
                Dynamic = "x" + foo,
                DynamicAlias = Runtime.Dynamic,
                Call = "x".toUpperCase(),
                CallAlias = Call
            }
            console.log(Runtime);
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(actualJs));
        Assert.Contains("Runtime[\"TemplateAlias\"] = Runtime.Template;", actualJs);
        Assert.Contains("Runtime[\"TemplateJoined\"] = Runtime.Template + 1;", actualJs);
        Assert.Contains("Runtime[\"DynamicAlias\"] = Runtime.Dynamic;", actualJs);
        Assert.Contains("Runtime[Runtime[\"CallAlias\"] = Runtime.Call] = \"CallAlias\";", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldFoldRuntimeEnumExponentiationAndAssertions()
    {
        var input = """
            enum Runtime {
                Pow = 2 ** 3,
                Truthy = true as any,
                Falsy = false satisfies any,
                Nil = null as any
            }
            console.log(Runtime.Pow, Runtime.Truthy, Runtime.Falsy, Runtime.Nil);
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("Runtime[Runtime[\"Pow\"] = 8] = \"Pow\";", output);
        Assert.Contains("Runtime[Runtime[\"Truthy\"] = true] = \"Truthy\";", output);
        Assert.Contains("Runtime[Runtime[\"Falsy\"] = false] = \"Falsy\";", output);
        Assert.Contains("Runtime[Runtime[\"Nil\"] = null] = \"Nil\";", output);
        Assert.DoesNotContain(" as ", output);
        Assert.DoesNotContain("satisfies", output);
    }

    [Fact]
    public void TypeScriptParserShouldFoldRuntimeEnumTemplateStringValuesLikeTypeScript60()
    {
        var input = """
            enum Runtime {
                Plain = `plain`,
                Interpolated = `x${1}`,
                Computed = `${1 + 2}`,
                Joined = "a" + `${"b"}`,
                Dynamic = `x${foo}`,
                DynamicJoined = "x" + foo,
                Asserted = "a" as const
            }
            console.log(Runtime.Plain, Runtime.Dynamic, Runtime.Asserted);
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("Runtime[\"Plain\"] = \"plain\";", output);
        Assert.Contains("Runtime[\"Interpolated\"] = \"x1\";", output);
        Assert.Contains("Runtime[\"Computed\"] = \"3\";", output);
        Assert.Contains("Runtime[\"Joined\"] = \"ab\";", output);
        Assert.Contains("Runtime[\"Dynamic\"] = `x${foo}`;", output);
        Assert.Contains("Runtime[\"DynamicJoined\"] = \"x\" + foo;", output);
        Assert.Contains("Runtime[Runtime[\"Asserted\"] = \"a\"] = \"Asserted\";", output);
    }

    [Fact]
    public void TypeScriptParserShouldQualifyRuntimeEnumReferencesInComputedInitializers()
    {
        var input = """
            enum Runtime {
                First = compute(),
                Second = First,
                Third = Second + 1,
                Constant = 1,
                Mixed = Constant + compute()
            }
            console.log(Runtime.Second, Runtime.Third, Runtime.Mixed);
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("Runtime[Runtime[\"Second\"] = Runtime.First] = \"Second\";", output);
        Assert.Contains("Runtime[Runtime[\"Third\"] = Runtime.Second + 1] = \"Third\";", output);
        Assert.Contains("Runtime[Runtime[\"Mixed\"] = Runtime.Constant + compute()] = \"Mixed\";", output);
        Assert.DoesNotContain("= First]", output);
        Assert.DoesNotContain("= Second + 1", output);
    }

    [Fact]
    public void TypeScriptParserShouldFoldQualifiedRuntimeEnumReferencesLikeTypeScript60()
    {
        var input = """
            enum Runtime {
                First = 1,
                Second = Runtime.First + 1,
                Third = globalThis.Runtime.Second + 1
            }
            console.log(Runtime.Second, Runtime.Third);
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(actualJs));
        Assert.Contains("Runtime[Runtime[\"Second\"] = 2] = \"Second\";", actualJs);
        Assert.Contains("Runtime[Runtime[\"Third\"] = 3] = \"Third\";", actualJs);
        Assert.DoesNotContain("Runtime.Runtime", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldPreserveRuntimeEnumBigIntAndNonFiniteValuesLikeTypeScript60()
    {
        var input = """
            enum Runtime {
                Big = 1n as any,
                BigAlias = Big as any,
                NotANumber = NaN,
                Positive = Infinity,
                Negative = -Infinity
            }
            console.log(Runtime.Big, Runtime.NotANumber, Runtime.Positive, Runtime.Negative);
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(actualJs));
        Assert.Contains("Runtime[Runtime[\"Big\"] = 1n] = \"Big\";", actualJs);
        Assert.Contains("Runtime[Runtime[\"BigAlias\"] = Runtime.Big] = \"BigAlias\";", actualJs);
        Assert.Contains("Runtime[Runtime[\"NotANumber\"] = NaN] = \"NotANumber\";", actualJs);
        Assert.Contains("Runtime[Runtime[\"Positive\"] = Infinity] = \"Positive\";", actualJs);
        Assert.Contains("Runtime[Runtime[\"Negative\"] = -Infinity] = \"Negative\";", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldFoldRuntimeEnumUnaryAndBitwiseValuesLikeTypeScript60()
    {
        var input = """
            enum Runtime {
                Negative = -1,
                Next,
                Inverted = ~Negative,
                Shifted = Negative << 2,
                Unsigned = Negative >>> 1
            }
            console.log(Runtime.Negative, Runtime.Next, Runtime.Inverted, Runtime.Shifted, Runtime.Unsigned);
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(actualJs));
        Assert.Contains("Runtime[Runtime[\"Negative\"] = -1] = \"Negative\";", actualJs);
        Assert.Contains("Runtime[Runtime[\"Next\"] = 0] = \"Next\";", actualJs);
        Assert.Contains("Runtime[Runtime[\"Inverted\"] = 0] = \"Inverted\";", actualJs);
        Assert.Contains("Runtime[Runtime[\"Shifted\"] = -4] = \"Shifted\";", actualJs);
        Assert.Contains("Runtime[Runtime[\"Unsigned\"] = 2147483647] = \"Unsigned\";", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldNormalizeRuntimeEnumNegativeZeroLikeTypeScript60()
    {
        var input = """
            enum Runtime {
                NegativeZero = -0,
                PositiveZero = +0,
                StringJoined = "x" + -0,
                Template = `${-0}`
            }
            console.log(Runtime);
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(actualJs));
        Assert.Contains("Runtime[Runtime[\"NegativeZero\"] = 0] = \"NegativeZero\";", actualJs);
        Assert.Contains("Runtime[\"StringJoined\"] = \"x0\";", actualJs);
        Assert.Contains("Runtime[\"Template\"] = \"0\";", actualJs);
        Assert.DoesNotContain("= -0", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldFoldCrossEnumRuntimeReferencesLikeTypeScript60()
    {
        var input = """
            enum Base {
                First = 1,
                Second = 2
            }
            enum Derived {
                FromDot = Base.First,
                FromIndex = Base["Second"],
                Mixed = Base.First + Base.Second
            }
            console.log(Base, Derived);
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(actualJs));
        Assert.Contains("Derived[Derived[\"FromDot\"] = 1] = \"FromDot\";", actualJs);
        Assert.Contains("Derived[Derived[\"FromIndex\"] = 2] = \"FromIndex\";", actualJs);
        Assert.Contains("Derived[Derived[\"Mixed\"] = 3] = \"Mixed\";", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldFoldRuntimeEnumForwardReferencesLikeTypeScript60()
    {
        var input = """
            enum Runtime {
                FromBare = Later,
                FromQualified = Runtime.Later + 2,
                Self = Runtime.Self,
                Later = 5,
                AfterSelf
            }
            console.log(Runtime);
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(actualJs));
        Assert.Contains("Runtime[Runtime[\"FromBare\"] = 0] = \"FromBare\";", actualJs);
        Assert.Contains("Runtime[Runtime[\"FromQualified\"] = 2] = \"FromQualified\";", actualJs);
        Assert.Contains("Runtime[Runtime[\"Self\"] = Runtime.Self] = \"Self\";", actualJs);
        Assert.Contains("Runtime[Runtime[\"Later\"] = 5] = \"Later\";", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldHandleConstEnumForwardReferencesLikeTypeScript60()
    {
        var bareInput = """
            const enum BareForward {
                FromBare = Later,
                Later = 5
            }
            console.log(BareForward.FromBare, BareForward.Later);
            """;
        var qualifiedInput = """
            const enum QualifiedForward {
                FromQualified = QualifiedForward.Later + 2,
                Later = 5
            }
            console.log(QualifiedForward.FromQualified, QualifiedForward.Later);
            """;

        var bareActualJs = PrintTypeScript(bareInput);
        var bareOracleJs = TranspileWithTypeScript60(bareInput).Replace("\"use strict\";\n", "");
        var qualifiedActualJs = PrintTypeScript(qualifiedInput);
        var qualifiedOracleJs = TranspileWithTypeScript60(qualifiedInput).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(bareOracleJs),
            DumpJavaScriptAstWithoutPositions(bareActualJs));
        Assert.Equal(DumpJavaScriptAstWithoutPositions(qualifiedOracleJs),
            DumpJavaScriptAstWithoutPositions(qualifiedActualJs));
        Assert.Contains("var BareForward;", bareActualJs);
        Assert.Contains("BareForward[BareForward[\"FromBare\"] = 0] = \"FromBare\";", bareActualJs);
        Assert.Contains("var QualifiedForward;", qualifiedActualJs);
        Assert.Contains("QualifiedForward[QualifiedForward[\"FromQualified\"] = 2] = \"FromQualified\";",
            qualifiedActualJs);
    }

    [Fact]
    public void TypeScriptParserShouldPreserveAllConstEnumsWhenOneNeedsRuntimeLikeTypeScript60()
    {
        var input = """
            const enum BareForward {
                FromBare = Later,
                Later = 5
            }
            const enum QualifiedForward {
                FromQualified = QualifiedForward.Later + 2,
                Later = 5
            }
            console.log(BareForward.FromBare, QualifiedForward.FromQualified);
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(actualJs));
        Assert.Contains("var BareForward;", actualJs);
        Assert.Contains("var QualifiedForward;", actualJs);
        Assert.Contains("BareForward[BareForward[\"FromBare\"] = 0] = \"FromBare\";", actualJs);
        Assert.Contains("QualifiedForward[QualifiedForward[\"FromQualified\"] = 2] = \"FromQualified\";", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldQualifyRuntimeEnumSelfReferencesLikeTypeScript60()
    {
        var input = """
            enum Runtime {
                Self = Self,
                FromSelf = Runtime.Self + 1,
                DynamicString = DynamicString + "x"
            }
            const enum ConstRuntime {
                Self = Self,
                QualifiedSelf = ConstRuntime.QualifiedSelf + 1
            }
            console.log(Runtime, ConstRuntime.Self, ConstRuntime.QualifiedSelf);
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(actualJs));
        Assert.Contains("Runtime[Runtime[\"Self\"] = Runtime.Self] = \"Self\";", actualJs);
        Assert.Contains("Runtime[\"DynamicString\"] = Runtime.DynamicString + \"x\";", actualJs);
        Assert.Contains("ConstRuntime[ConstRuntime[\"Self\"] = ConstRuntime.Self] = \"Self\";", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldFoldNamespaceRuntimeEnumSelfAndForwardReferencesLikeTypeScript60()
    {
        var input = """
            namespace N {
                export enum Runtime {
                    Self = Self,
                    Forward = Later + 2,
                    Later = 5
                }
                export const enum PreservedConst {
                    Self = Self,
                    Forward = Later,
                    Later = 5
                }
                export const selected = Runtime.Forward + PreservedConst.Forward;
            }
            console.log(N.selected);
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(actualJs));
        Assert.Contains("Runtime[Runtime[\"Self\"] = Runtime.Self] = \"Self\";", actualJs);
        Assert.Contains("Runtime[Runtime[\"Forward\"] = 2] = \"Forward\";", actualJs);
        Assert.Contains("PreservedConst[PreservedConst[\"Self\"] = PreservedConst.Self] = \"Self\";", actualJs);
        Assert.Contains("PreservedConst[PreservedConst[\"Forward\"] = 0] = \"Forward\";", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldPreserveNamespaceRuntimeEnumDynamicStringAliasesLikeTypeScript60()
    {
        var input = """
            namespace N {
                export enum Runtime {
                    Template = `${foo}`,
                    TemplateAlias = Template,
                    TemplateJoined = Runtime.Template + 1
                }
                export const enum PreservedConst {
                    Template = `${bar}`,
                    TemplateAlias = Template,
                    TemplateJoined = PreservedConst.Template + 1
                }
                export const selected = Runtime.TemplateAlias + PreservedConst.TemplateAlias;
            }
            console.log(N.selected);
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(actualJs));
        Assert.Contains("Runtime[\"TemplateAlias\"] = Runtime.Template;", actualJs);
        Assert.Contains("Runtime[\"TemplateJoined\"] = Runtime.Template + 1;", actualJs);
        Assert.Contains("PreservedConst[\"TemplateAlias\"] = PreservedConst.Template;", actualJs);
        Assert.Contains("PreservedConst[\"TemplateJoined\"] = PreservedConst.Template + 1;", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldPreserveNamespaceBodiesWithRegexBracesLikeTypeScript60()
    {
        var input = """
            namespace N {
                export const close = /}/g;
                export const open = /{/g;
                export const escaped = /\}/;
                export const selected = close.test("}") && open.test("{") && escaped.test("}");
            }
            console.log(N.selected);
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(actualJs));
        Assert.Contains("N.close = /}/g;", actualJs);
        Assert.Contains("N.open = /{/g;", actualJs);
        Assert.Contains("N.escaped = /\\}/;", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldFoldKnownMembersFromPartiallyDynamicRuntimeEnumsLikeTypeScript60()
    {
        var input = """
            enum Base {
                First = 1,
                Dynamic = compute()
            }
            enum Derived {
                FromKnown = Base.First + 1,
                FromDynamic = Base.Dynamic + 1
            }
            console.log(Base, Derived);
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(actualJs));
        Assert.Contains("Derived[Derived[\"FromKnown\"] = 2] = \"FromKnown\";", actualJs);
        Assert.Contains("Derived[Derived[\"FromDynamic\"] = Base.Dynamic + 1] = \"FromDynamic\";", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldFoldKnownMembersFromPartialNamespaceRuntimeEnumsLikeTypeScript60()
    {
        var input = """
            namespace Runtime {
                export enum Base {
                    First = 1,
                    Dynamic = compute()
                }
                export enum Derived {
                    FromKnown = Base.First + 1,
                    FromDynamic = Base.Dynamic + 1
                }
            }
            enum Outside {
                FromKnown = Runtime.Base.First + 1,
                FromDynamic = Runtime.Base.Dynamic + 1
            }
            console.log(Runtime, Outside);
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(actualJs));
        Assert.Contains("Derived[Derived[\"FromKnown\"] = 2] = \"FromKnown\";", actualJs);
        Assert.Contains("Derived[Derived[\"FromDynamic\"] = Base.Dynamic + 1] = \"FromDynamic\";", actualJs);
        Assert.Contains("Outside[Outside[\"FromKnown\"] = 2] = \"FromKnown\";", actualJs);
        Assert.Contains("Outside[Outside[\"FromDynamic\"] = Runtime.Base.Dynamic + 1] = \"FromDynamic\";", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldFoldNamespaceRuntimeEnumReferencesLikeTypeScript60()
    {
        var input = """
            namespace Runtime {
                export enum Base {
                    First = 1,
                    Second = 2
                }
                export enum Derived {
                    FromLocal = Base.First + Base.Second
                }
            }
            enum Outside {
                FromNamespace = Runtime.Base.First + 1
            }
            console.log(Runtime, Outside);
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(actualJs));
        Assert.Contains("Derived[Derived[\"FromLocal\"] = 3] = \"FromLocal\";", actualJs);
        Assert.Contains("Outside[Outside[\"FromNamespace\"] = 2] = \"FromNamespace\";", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldFoldMergedRuntimeEnumReferencesLikeTypeScript60()
    {
        var input = """
            enum Runtime {
                First = 1
            }
            enum Runtime {
                Second = First + 1,
                Third = Runtime.First + Runtime.Second
            }
            namespace Feature {
                export enum Mode {
                    A = 1
                }
            }
            namespace Feature {
                export enum Mode {
                    B = Mode.A + 1,
                    C = Feature.Mode.B + 1
                }
            }
            console.log(Runtime, Feature);
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(actualJs));
        Assert.Contains("Runtime[Runtime[\"Second\"] = 2] = \"Second\";", actualJs);
        Assert.Contains("Runtime[Runtime[\"Third\"] = 3] = \"Third\";", actualJs);
        Assert.Contains("Mode[Mode[\"B\"] = 2] = \"B\";", actualJs);
        Assert.Contains("Mode[Mode[\"C\"] = 3] = \"C\";", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldFoldKnownMembersFromPartialMergedRuntimeEnumsLikeTypeScript60()
    {
        var input = """
            enum Runtime {
                First = 1,
                Dynamic = compute()
            }
            enum Runtime {
                FromKnown = Runtime.First + 1,
                FromDynamic = Runtime.Dynamic + 1
            }
            console.log(Runtime);
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(actualJs));
        Assert.Contains("Runtime[Runtime[\"FromKnown\"] = 2] = \"FromKnown\";", actualJs);
        Assert.Contains("Runtime[Runtime[\"FromDynamic\"] = Runtime.Dynamic + 1] = \"FromDynamic\";", actualJs);
        Assert.Equal(1, CountOccurrences(actualJs, "var Runtime;"));
    }

    [Fact]
    public void TypeScriptParserShouldFoldLocalRuntimeEnumReferencesLikeTypeScript60()
    {
        var input = """
            enum Runtime {
                Outer = 1
            }
            function read() {
                enum Runtime {
                    Local = 2
                }
                enum Derived {
                    FromLocal = Runtime.Local + 1
                }
                return Derived.FromLocal;
            }
            enum After {
                FromOuter = Runtime.Outer + 1
            }
            console.log(read(), After.FromOuter);
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(actualJs));
        Assert.Contains("Derived[Derived[\"FromLocal\"] = 3] = \"FromLocal\";", actualJs);
        Assert.Contains("After[After[\"FromOuter\"] = 2] = \"FromOuter\";", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldFoldBlockLocalRuntimeEnumReferencesLikeTypeScript60()
    {
        var input = """
            enum Runtime {
                Outer = 1
            }
            if (enabled) {
                enum Runtime {
                    Local = 2
                }
                enum Derived {
                    FromLocal = Runtime.Local + 1
                }
                use(Derived);
            }
            enum After {
                FromOuter = Runtime.Outer + 1
            }
            console.log(Runtime, After);
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(actualJs));
        Assert.Contains("Derived[Derived[\"FromLocal\"] = 3] = \"FromLocal\";", actualJs);
        Assert.Contains("After[After[\"FromOuter\"] = 2] = \"FromOuter\";", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldFoldLiteralRuntimeEnumMemberReferencesLikeTypeScript60()
    {
        var input = """
            enum Runtime {
                "a-b" = 1,
                Escaped = Runtime["a-b"] + 1,
                ["computed"] = 3,
                FromComputed = Runtime.computed + 1,
                [`templated`] = 5,
                FromTemplate = Runtime.templated + 1,
                0 = 7,
                Numeric = Runtime[0] + 1
            }
            console.log(Runtime.Escaped, Runtime.FromComputed, Runtime.FromTemplate, Runtime.Numeric);
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(actualJs));
        Assert.Contains("Runtime[Runtime[\"Escaped\"] = 2] = \"Escaped\";", actualJs);
        Assert.Contains("Runtime[Runtime[\"FromComputed\"] = 4] = \"FromComputed\";", actualJs);
        Assert.Contains("Runtime[Runtime[\"FromTemplate\"] = 6] = \"FromTemplate\";", actualJs);
        Assert.Contains("Runtime[Runtime[\"Numeric\"] = Runtime[0] + 1] = \"Numeric\";", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldParseComputedEnumNamesWithNestedDelimitersLikeTypeScript60()
    {
        var input = """
            enum Runtime {
                ["]"] = 1,
                [`}`] = 2,
                FromString = Runtime["]"],
                FromTemplate = Runtime["}"]
            }
            console.log(Runtime.FromString, Runtime.FromTemplate);
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(actualJs));
        Assert.Contains("Runtime[Runtime[\"]\"] = 1] = \"]\";", actualJs);
        Assert.Contains("Runtime[Runtime[`}`] = 2] = `}`;", actualJs);
        Assert.Contains("Runtime[Runtime[\"FromString\"] = 1] = \"FromString\";", actualJs);
        Assert.Contains("Runtime[Runtime[\"FromTemplate\"] = 2] = \"FromTemplate\";", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldFoldCrossEnumLiteralMemberReferencesLikeTypeScript60()
    {
        var input = """
            enum Base {
                "a-b" = 1,
                ["computed"] = 2,
                [`templated`] = 3,
                "a\nb" = 4,
                default = 5,
                0 = 6
            }
            enum Derived {
                FromString = Base["a-b"] + 1,
                FromComputed = Base.computed + 1,
                FromTemplate = Base.templated + 1,
                FromEscaped = Base["a\nb"] + 1,
                FromKeyword = Base.default + 1,
                FromNumeric = Base[0] + 1
            }
            console.log(Base, Derived);
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(actualJs));
        Assert.Contains("Derived[Derived[\"FromString\"] = 2] = \"FromString\";", actualJs);
        Assert.Contains("Derived[Derived[\"FromComputed\"] = 3] = \"FromComputed\";", actualJs);
        Assert.Contains("Derived[Derived[\"FromTemplate\"] = 4] = \"FromTemplate\";", actualJs);
        Assert.Contains("Derived[Derived[\"FromEscaped\"] = 5] = \"FromEscaped\";", actualJs);
        Assert.Contains("Derived[Derived[\"FromKeyword\"] = 6] = \"FromKeyword\";", actualJs);
        Assert.Contains("Derived[Derived[\"FromNumeric\"] = Base[0] + 1] = \"FromNumeric\";", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldFoldGlobalThisAndOptionalEnumReferencesLikeTypeScript60()
    {
        var input = """
            enum Base {
                A = 1
            }
            enum Derived {
                FromGlobalThis = globalThis.Base.A + 1,
                FromOptionalDot = Base?.A + 1,
                FromOptionalIndex = Base?.["A"] + 1,
                FromUnrelatedQualifier = Other.Base.A + 1
            }
            console.log(Base, Derived);
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(actualJs));
        Assert.Contains("Derived[Derived[\"FromGlobalThis\"] = 2] = \"FromGlobalThis\";", actualJs);
        Assert.Contains("Derived[Derived[\"FromOptionalDot\"] = 2] = \"FromOptionalDot\";", actualJs);
        Assert.Contains("Derived[Derived[\"FromOptionalIndex\"] = 2] = \"FromOptionalIndex\";", actualJs);
        Assert.Contains("Derived[Derived[\"FromUnrelatedQualifier\"] = Other.Base.A + 1] = \"FromUnrelatedQualifier\";", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldPreserveAssertedEnumReferencesLikeTypeScript60()
    {
        var input = """
            enum Base {
                A = 1
            }
            enum Derived {
                FromParen = (Base.A) + 1,
                FromNestedParen = ((Base)).A + 1,
                FromNonNullObject = Base!.A + 1,
                FromNonNullMember = Base.A! + 1,
                FromAssertedObject = (Base as any).A + 1,
                FromAssertedMember = (Base.A as any) + 1,
                FromSatisfiesObject = (Base satisfies any).A + 1,
                FromSatisfiesMember = (Base.A satisfies any) + 1,
                FromComma = (side(), Base.A) + 1
            }
            console.log(Base, Derived);
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(actualJs));
        Assert.Contains("Derived[Derived[\"FromParen\"] = 2] = \"FromParen\";", actualJs);
        Assert.Contains("Derived[Derived[\"FromNestedParen\"] = Base.A + 1] = \"FromNestedParen\";", actualJs);
        Assert.Contains("Derived[Derived[\"FromNonNullObject\"] = Base.A + 1] = \"FromNonNullObject\";", actualJs);
        Assert.Contains("Derived[Derived[\"FromNonNullMember\"] = Base.A + 1] = \"FromNonNullMember\";", actualJs);
        Assert.Contains("Derived[Derived[\"FromAssertedObject\"] = Base.A + 1] = \"FromAssertedObject\";", actualJs);
        Assert.Contains("Derived[Derived[\"FromAssertedMember\"] = Base.A + 1] = \"FromAssertedMember\";", actualJs);
        Assert.Contains("Derived[Derived[\"FromSatisfiesObject\"] = Base.A + 1] = \"FromSatisfiesObject\";", actualJs);
        Assert.Contains("Derived[Derived[\"FromSatisfiesMember\"] = Base.A + 1] = \"FromSatisfiesMember\";", actualJs);
        Assert.Contains("Derived[Derived[\"FromComma\"] = (side(), Base.A) + 1] = \"FromComma\";", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldParseKeywordRuntimeEnumMemberNames()
    {
        var input = """
            enum Runtime {
                true = 1,
                false = true + 1,
                null = false + 1,
                default = null + 1,
                class = 5,
                await = 6
            }
            console.log(Runtime.true, Runtime.false, Runtime.null, Runtime.default, Runtime.class, Runtime.await);
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("Runtime[Runtime[\"true\"] = 1] = \"true\";", output);
        Assert.Contains("Runtime[Runtime[\"false\"] = 2] = \"false\";", output);
        Assert.Contains("Runtime[Runtime[\"null\"] = 3] = \"null\";", output);
        Assert.Contains("Runtime[Runtime[\"default\"] = 4] = \"default\";", output);
        Assert.Contains("Runtime[Runtime[\"class\"] = 5] = \"class\";", output);
        Assert.Contains("Runtime[Runtime[\"await\"] = 6] = \"await\";", output);
    }

    [Fact]
    public void TypeScriptParserShouldEraseTypeOnlyAsImportExportSpecifiers()
    {
        var input = """
            import { type as, type as as Alias, value } from "module";
            export { type as, type as as ExportedAlias, value } from "module";
            console.log(value, Alias, ExportedAlias);
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("import { value } from \"module\";", output);
        Assert.Contains("export { value } from \"module\";", output);
        Assert.Contains("console.log(value, Alias, ExportedAlias);", output);
        Assert.DoesNotContain("type as", output);
    }

    [Fact]
    public void TypeScriptParserShouldPreserveRuntimeTypeAsImportExportSpecifiers()
    {
        var input = """
            import { type as Alias } from "module";
            export { type as ExportedAlias } from "module";
            console.log(Alias);
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("import { type as Alias } from \"module\";", output);
        Assert.Contains("export { type as ExportedAlias } from \"module\";", output);
        Assert.Contains("console.log(Alias);", output);
    }

    [Fact]
    public void TypeScriptParserShouldPreserveRuntimeTypeExpressionStatements()
    {
        var input = """
            import { type } from "module";
            type;
            type
            Foo = string;
            type Alias = string;
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("import { type } from \"module\";", output);
        Assert.Contains("type;", output);
        Assert.Contains("Foo = string;", output);
        Assert.DoesNotContain("Alias", output);
    }

    [Fact]
    public void TypeScriptParserShouldEraseParenthesizedSatisfiesAndAsExpressions()
    {
        var input = """
            const value = ({ a: 1 } satisfies { a: number }) as { a: number };
            console.log(value.a);
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("const value = {", output);
        Assert.Contains("a: 1", output);
        Assert.DoesNotContain("satisfies", output);
        Assert.DoesNotContain("as {", output);
        Assert.Contains("console.log(value.a);", output);
    }

    [Fact]
    public void TypeScriptParserShouldEraseThisParametersFromArrowFunctions()
    {
        var input = """
            const first = (this: void, value: string) => value;
            const second = (this: void) => 1;
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("const first = value => value;", output);
        Assert.Contains("const second = () => 1;", output);
        Assert.DoesNotContain("this:", output);
    }

    [Fact]
    public void TypeScriptParserShouldEraseTypedDestructuringParameterAnnotationsLikeTypeScript60()
    {
        var input = """
            function first({ a }?: { a: string }) {
                return a;
            }
            function second({ a }: { a: string } = { a: "x" }) {
                return a;
            }
            const third = ([a, b]: [string, number] = ["x", 1]) => a;
            function fourth(...values: [string, number]) {
                return values;
            }
            const fifth = (this: void, { a }: { a: string }) => a;
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("function first({a})", output);
        Assert.Contains("function second({a} = {", output);
        Assert.Contains("const third = ([a, b] = [", output);
        Assert.Contains("function fourth(...values)", output);
        Assert.Contains("const fifth = ({a}) => a;", output);
        Assert.DoesNotContain(": {", output);
        Assert.DoesNotContain(": [", output);
        Assert.DoesNotContain("this:", output);
    }

    [Fact]
    public void TypeScriptParserShouldEraseStaticParameterModifiers()
    {
        var input = """
            const public = 1;
            let private = 2;
            var protected = 3;
            function first(static value: string) {
                return value;
            }
            function withDefault(static value = 1) {
                return value;
            }
            function withRest(static ...values: string[]) {
                return values.length;
            }
            function contextual(readonly: string, public: number) {
                return readonly + public;
            }
            const second = (static value: string) => value;
            const third = (static value = 1) => value;
            const fourth = (static ...values: string[]) => values.length;
            class Service {
                constructor(static value: string) {
                    console.log(value);
                }
                method(static value = 1) {
                    return value;
                }
                rest(static ...values: string[]) {
                    return values.length;
                }
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("const public = 1;", output);
        Assert.Contains("let private = 2;", output);
        Assert.Contains("var protected = 3;", output);
        Assert.Contains("function first(value)", output);
        Assert.Contains("function withDefault(value = 1)", output);
        Assert.Contains("function withRest(...values)", output);
        Assert.Contains("function contextual(readonly, public)", output);
        Assert.Contains("return readonly + public;", output);
        Assert.Contains("const second = value => value;", output);
        Assert.Contains("const third = (value = 1) => value;", output);
        Assert.Contains("const fourth = (...values) => values.length;", output);
        Assert.Contains("constructor(value)", output);
        Assert.Contains("method(value = 1)", output);
        Assert.Contains("rest(...values)", output);
        Assert.DoesNotContain("static value", output);
        Assert.DoesNotContain("static ...values", output);
    }

    [Fact]
    public void TypeScriptParserShouldEraseAsyncArrowReturnTypes()
    {
        var input = """
            const first = async (value: string): Promise<string> => value;
            const second = async (value: string): value is string => true;
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("const first = async value => value;", output);
        Assert.Contains("const second = async value => true;", output);
        Assert.DoesNotContain("Promise", output);
        Assert.DoesNotContain("value is", output);
    }

    [Fact]
    public void TypeScriptParserShouldEraseObjectMethodTypeParameters()
    {
        var input = """
            const obj = {
                method<T>(value: T): T {
                    return value;
                },
                async load<T>(value: T): Promise<T> {
                    return value;
                },
                async *items<T>(): AsyncIterable<T> {
                    yield value;
                },
                readonly async *readonlyItems<T>(): AsyncIterable<T> {
                    yield value;
                },
                readonly *readonlyGenerator<T>(): Iterable<T> {
                    yield value;
                },
                readonly async readonlyLoad<T>(value: T): Promise<T> {
                    return value;
                },
                readonly get readonlyValue(): T {
                    return value;
                },
                readonly set readonlyValue(value: T) {
                    this.value = value;
                }
            } satisfies Shape;
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("method(value)", output);
        Assert.Contains("async load(value)", output);
        Assert.Contains("async* items()", output);
        Assert.Contains("async* readonlyItems()", output);
        Assert.Contains("* readonlyGenerator()", output);
        Assert.Contains("async readonlyLoad(value)", output);
        Assert.Contains("get readonlyValue()", output);
        Assert.Contains("set readonlyValue(value)", output);
        Assert.DoesNotContain("<T>", output);
        Assert.DoesNotContain("Promise", output);
        Assert.DoesNotContain("satisfies", output);
    }

    [Fact]
    public void TypeScriptParserShouldEraseObjectPropertyModifiers()
    {
        var input = """
            const { public } = source;
            const private = 1;
            function read(readonly: number) {
                return readonly;
            }
            const obj = {
                public,
                readonly value: 1,
                public other: 2,
                accessor ready: true,
                declare name: "service",
                override count: 3,
                readonly ["]"]: 4,
                declare [`}`]: 5
            };
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("const {public: public} = source;", output);
        Assert.Contains("const private = 1;", output);
        Assert.Contains("function read(readonly)", output);
        Assert.Contains("return readonly;", output);
        Assert.Contains("public: public", output);
        Assert.Contains("value: 1", output);
        Assert.Contains("other: 2", output);
        Assert.Contains("ready: true", output);
        Assert.Contains("name: \"service\"", output);
        Assert.Contains("count: 3", output);
        Assert.Contains("\"]\": 4", output);
        Assert.Contains("[`}`]: 5", output);
        Assert.DoesNotContain("readonly value", output);
        Assert.DoesNotContain("public other", output);
        Assert.DoesNotContain("accessor", output);
        Assert.DoesNotContain("declare", output);
        Assert.DoesNotContain("override", output);
    }

    [Fact]
    public void TypeScriptParserShouldParseGenericObjectAccessorsLikeTypeScript60()
    {
        var input = """
            const obj = {
                get value<T>(): T {
                    return value;
                },
                set value<T>(value: T) {}
            };
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("get value()", output);
        Assert.Contains("set value(value)", output);
        Assert.DoesNotContain("<T>", output);
    }

    [Fact]
    public void TypeScriptParserShouldIgnoreDecoratorsOnThisParameters()
    {
        var input = """
            function dec(target: unknown, key: string | undefined, index: number) {}
            class Service {
                method(@dec this: Service, @dec value: string) {}
                constructor(@dec this: Service, @dec name: string) {}
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("method(value)", output);
        Assert.Contains("constructor(name)", output);
        Assert.Contains("__decorate([ __param(0, dec) ], Service.prototype, \"method\", null);", output);
        Assert.Contains("Service = __decorate([ __param(0, dec) ], Service);", output);
        Assert.DoesNotContain("__param(0, dec), __param(0, dec)", output);
    }

    [Fact]
    public void TypeScriptParserShouldEraseSatisfiesAndConstAssertionsAtExpressionBoundaries()
    {
        var input = """
            const defs = [{ id: "a" } satisfies { id: string }, { id: "b" } as const];
            export const cfg = { nested: { ok: true } } satisfies { nested: { ok: boolean } };
            const value = ({ a: 1 } satisfies { a: number }).a;
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("const defs = [", output);
        Assert.Contains("id: \"a\"", output);
        Assert.Contains("id: \"b\"", output);
        Assert.Contains("export const cfg = {", output);
        Assert.Contains("const value = {", output);
        Assert.Contains("}.a;", output);
        Assert.DoesNotContain("satisfies", output);
        Assert.DoesNotContain("as const", output);
    }

    [Fact]
    public void TypeScriptParserShouldEraseStringAndTemplateLiteralTypeAssertions()
    {
        var input = """
            const first = value as "=";
            const second = value satisfies ";";
            const third: `}` = value;
            function use(value: "{" | ")"): `;` {
                return value;
            }
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("const first = value;", output);
        Assert.Contains("const second = value;", output);
        Assert.Contains("const third = value;", output);
        Assert.Contains("function use(value) {", output);
        Assert.Contains("return value;", output);
        Assert.DoesNotContain(" as ", output);
        Assert.DoesNotContain("satisfies", output);
        Assert.DoesNotContain("`}`", output);
    }

    [Fact]
    public void TypeScriptParserShouldEraseChainedAsAndSatisfiesAssertions()
    {
        var input = """
            const first = foo satisfies Bar as Baz;
            const second = foo as Bar satisfies Baz;
            const third = flag satisfies boolean ? yes : no;
            const fourth = value satisfies T extends U ? A : B;
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("const first = foo;", output);
        Assert.Contains("const second = foo;", output);
        Assert.Contains("const third = flag ? yes : no;", output);
        Assert.Contains("const fourth = value;", output);
        Assert.DoesNotContain("satisfies", output);
        Assert.DoesNotContain(" as ", output);
    }

    [Fact]
    public void TypeScriptParserShouldEraseAssertionsInConditionalBranchesLikeTypeScript60()
    {
        var input = """
            const value = flag ? first as number : second as number;
            use(value);
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(actualJs));
        Assert.Contains("const value = flag ? first : second;", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldStopConditionalTypeAssertionsAtOuterTernaryColonLikeTypeScript60()
    {
        var input = """
            const value = flag
                ? first satisfies T extends U ? A extends B ? C : D : E
                : second satisfies X extends Y ? C : D;
            use(value);
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(actualJs));
        Assert.Contains("const value = flag ? first : second;", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldPreserveOperatorsAfterAsAndSatisfiesAssertions()
    {
        var input = """
            const first = a as number + b;
            const second = a satisfies number * b;
            const third = a + b as number + c;
            const fourth = [a as number + b, c];
            const fifth = a as number < b;
            const sixth = a as Array<number> < b;
            const seventh = a satisfies number >= b;
            const eighth = a as string ?? b;
            const ninth = a as string || b;
            const tenth = a as string === b;
            const eleventh = a as string in b;
            const twelfth = a as object instanceof C;
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("const first = a + b;", output);
        Assert.Contains("const second = a * b;", output);
        Assert.Contains("const third = a + b + c;", output);
        Assert.Contains("const fourth = [", output);
        Assert.Contains("a + b", output);
        Assert.Contains("const fifth = a < b;", output);
        Assert.Contains("const sixth = a < b;", output);
        Assert.Contains("const seventh = a >= b;", output);
        Assert.Contains("const eighth = a ?? b;", output);
        Assert.Contains("const ninth = a || b;", output);
        Assert.Contains("const tenth = a === b;", output);
        Assert.Contains("const eleventh = a in b;", output);
        Assert.Contains("const twelfth = a instanceof C;", output);
        Assert.DoesNotContain(" as ", output);
        Assert.DoesNotContain("satisfies", output);
    }

    [Fact]
    public void TypeScriptParserShouldEraseNonNullAssertionsInMemberChains()
    {
        var input = """
            declare const foo: any;
            foo!.bar;
            foo?.bar!();
            (foo as any)!.bar;
            """;

        var output = PrintTypeScript(input);

        Assert.Contains("foo.bar;", output);
        Assert.Contains("foo?.bar();", output);
        Assert.DoesNotContain("!", output);
        Assert.DoesNotContain("as any", output);
    }

    [Fact]
    public void TypeScriptParserShouldEraseNonNullGenericCallChainsLikeTypeScript60()
    {
        var input = """
            const value = service!.load!<string>()!.name;
            use(value);
            """;

        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs),
            DumpJavaScriptAstWithoutPositions(actualJs));
        Assert.Contains("const value = service.load().name;", actualJs);
    }

    [Fact]
    public void TypeScriptParserShouldParseTsxGenericArrowExpressionBodies()
    {
        var input = """
            const first = <T,>(value: T) => <div>{value}</div>;
            const second = <T extends {}>(value: T) => <span />;
            const third = <const T extends string>(value: T) => <button />;
            const fourth = <T = string,>(value?: T) => <section />;
            """;

        var output = PrintTypeScriptTsx(input);

        Assert.Contains("const first = value => <div>{value}</div>;", output);
        Assert.Contains("const second = value => <span />;", output);
        Assert.Contains("const third = value => <button />;", output);
        Assert.Contains("const fourth = value => <section />;", output);
        Assert.DoesNotContain("=> {", output);
        Assert.DoesNotContain("extends", output);
    }

    [Fact]
    public void TypeScriptParserShouldMatchTypeScript60OracleAstForEs2022()
    {
        var input = File.ReadAllText("Input/TypeScript/Oracle/es2022-oracle.ts");
        var actualJs = PrintTypeScript(input);
        var oracleJs = TranspileWithTypeScript60(input).Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs, removeTypeScriptHelpers: true),
            DumpJavaScriptAstWithoutPositions(actualJs, removeTypeScriptHelpers: true));
    }

    [Fact]
    public void TypeScriptParserShouldMatchTypeScript60TsxOracleAstForEs2022()
    {
        var input = File.ReadAllText("Input/TypeScript/Oracle/tsx-es2022-oracle.tsx");
        var actualJs = PrintTypeScriptTsx(input);
        var oracleJs = TranspileWithTypeScript60(input, "tsx-es2022-oracle.tsx").Replace("\"use strict\";\n", "");

        Assert.Equal(DumpJavaScriptAstWithoutPositions(oracleJs, parseJsx: true),
            DumpJavaScriptAstWithoutPositions(actualJs, parseJsx: true));
    }

    public static (string outAst, string outNiceJs, string outNiceJsMap, string outMinJs, string outMinJsMap) TypeScriptParserTestCore(
        TypeScriptParserTestData testData)
    {
        var comments = new List<(bool block, string content, SourceLocation location)>();
        var commentListener = new CommentListener();
        var toplevel = TypeScriptParser.Parse(testData.Input, new Options
        {
            SourceFile = testData.SourceName,
            ParseJSX = testData.SourceName.EndsWith(".tsx"),
            SourceType = SourceType.Module,
            OnComment = (block, content, location) =>
            {
                commentListener.OnComment(block, content, location);
                comments.Add((block, content, location));
            }
        });
        commentListener.Walk(toplevel);

        var strSink = new StringLineSink();
        toplevel.FigureOutScope();
        var dumper = new DumpAst(new AstDumpWriter(strSink));
        dumper.Walk(toplevel);
        foreach (var (block, content, location) in comments)
        {
            strSink.Print(
                $"{(block ? "Block" : "Line")} Comment ({location.Start.ToShortString()}-{location.End.ToShortString()}): {content}");
        }

        var outAst = strSink.ToString();
        var outNiceJsBuilder = new SourceMapBuilder();
        toplevel.PrintToBuilder(outNiceJsBuilder, new OutputOptions { Beautify = true });
        outNiceJsBuilder.AddText(
            $"//# sourceMappingURL={PathUtils.ChangeExtension(testData.SourceName, "nicejs.map")}");
        var outNiceJs = outNiceJsBuilder.Content();
        var outNiceJsMap = outNiceJsBuilder.Build(".", ".").ToString();

        var outMinJsBuilder = new SourceMapBuilder();
        toplevel.PrintToBuilder(outMinJsBuilder, new OutputOptions());
        outMinJsBuilder.AddText(
            $"//# sourceMappingURL={PathUtils.ChangeExtension(testData.SourceName, "minjs.map")}");
        var outMinJs = outMinJsBuilder.Content();
        var outMinJsMap = outMinJsBuilder.Build(".", ".").ToString();

        return (outAst, outNiceJs, outNiceJsMap, outMinJs, outMinJsMap);
    }

    static string PrintTypeScript(string input)
    {
        var toplevel = TypeScriptParser.Parse(input, new Options
        {
            SourceFile = "regression.ts",
            SourceType = SourceType.Module
        });
        var builder = new SourceMapBuilder();
        toplevel.PrintToBuilder(builder, new OutputOptions { Beautify = true });
        return builder.Content();
    }

    static string PrintTypeScriptTsx(string input)
    {
        var toplevel = TypeScriptParser.ParseTsx(input, new Options
        {
            SourceFile = "regression.tsx",
            SourceType = SourceType.Module
        });
        var builder = new SourceMapBuilder();
        toplevel.PrintToBuilder(builder, new OutputOptions { Beautify = true });
        return builder.Content();
    }

    static string TranspileWithTypeScript60(string input, string fileName = "es2022-oracle.ts")
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
                jsx: ts.JsxEmit.Preserve,
                experimentalDecorators: true,
                useDefineForClassFields: true
              },
              fileName: process.argv[2]
            });
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
        startInfo.ArgumentList.Add(fileName);

        using var process = Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
        Assert.Empty(error);
        return output;
    }

    static string DumpJavaScriptAstWithoutPositions(string input, bool removeTypeScriptHelpers = false,
        bool parseJsx = false)
    {
        var toplevel = Parser.Parse(input, new Options
        {
            SourceFile = "oracle.js",
            SourceType = SourceType.Module,
            EcmaVersion = 2022,
            ParseJSX = parseJsx
        });
        if (removeTypeScriptHelpers)
            toplevel = (AstToplevel)new TypeScriptHelperEraseTransformer().Transform(toplevel);
        var sink = new StringLineSink();
        var dumper = new DumpAst(new AstDumpWriter(sink, withoutPositions: true));
        dumper.Walk(toplevel);
        return sink.ToString();
    }

    public static string NormalizeValidateTsJavaScript(string input)
    {
        var toplevel = Parser.Parse(input, new Options
        {
            SourceFile = "oracle.js",
            SourceType = SourceType.Module,
            EcmaVersion = 2022
        });
        toplevel = (AstToplevel)new EsModuleTaggingEraseTransformer().Transform(toplevel);
        return toplevel.PrintToString(new OutputOptions { Beautify = true });
    }

    static int CountOccurrences(string value, string pattern)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += pattern.Length;
        }

        return count;
    }

    sealed class TypeScriptHelperEraseTransformer : TreeTransformer
    {
        protected override AstNode? Before(AstNode node, bool inList)
        {
            if (node is AstVar varNode)
            {
                for (var i = 0; i < varNode.Definitions.Count; i++)
                {
                    if (varNode.Definitions[(uint)i].Name is AstSymbol
                        {
                            Name: "__rest" or "__assign" or "__addDisposableResource" or "__disposeResources"
                            or "__decorate" or "__param"
                        })
                    {
                        varNode.Definitions.RemoveAt(i);
                        i--;
                    }
                }
                return varNode.Definitions.Count == 0 ? Remove : null;
            }
            return null;
        }

        protected override AstNode? After(AstNode node, bool inList)
        {
            return null;
        }
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

    [Theory]
    [ValidateTSTestDataProvider("Input/TypeScript/ValidateTS")]
    public void ValidateTSParserShouldProduceExpectedJs(ValidateTSTestData testData)
    {
        var isTsx = testData.SourceName.EndsWith(".tsx");
        var toplevel = isTsx
            ? TypeScriptParser.ParseTsx(testData.Input, new Options
            {
                SourceFile = testData.SourceName,
                SourceType = SourceType.Module
            })
            : TypeScriptParser.Parse(testData.Input, new Options
            {
                SourceFile = testData.SourceName,
                SourceType = SourceType.Module
            });
        var builder = new SourceMapBuilder();
        toplevel.PrintToBuilder(builder, new OutputOptions { Beautify = true });
        var expected = NormalizeValidateTsJavaScript(testData.ExpectedJs);
        var output = NormalizeValidateTsJavaScript(builder.Content());

        if (output != expected)
        {
            var wrongDir = "Wrong/TypeScript/ValidateTS";
            Directory.CreateDirectory(wrongDir);
            var wrongFile = Path.Combine(wrongDir, testData.SourceName + ".expected.js");
            File.WriteAllText(wrongFile, output);
        }

        Assert.Equal(expected, output);
    }
}
