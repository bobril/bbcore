using Lib.DiskCache;
using Lib.TSCompiler;
using Xunit;

namespace Lib.Test;

public class PackageJsonExportsTests
{
    static TSProject LoadProject(string packageJson)
    {
        var fs = new InMemoryFs();
        fs.WriteAllUtf8("/package.json", packageJson);
        var dc = new DiskCache.DiskCache(fs, () => fs);
        var project = new TSProject
        {
            DiskCache = dc,
            Owner = (IDirectoryCache)dc.TryGetItem("/")!
        };
        project.ProjectOptions = new()
        {
            Owner = project,
            ForbiddenDependencyUpdate = true
        };
        project.LoadProjectJson(true, null);
        return project;
    }

    [Fact]
    public void ResolvesSubpathWithNestedConditionsLikeEntities()
    {
        var project = LoadProject("""
            {
              "exports": {
                ".": {
                  "import": { "types": "./dist/esm/index.d.ts", "default": "./dist/esm/index.js" },
                  "require": { "types": "./dist/commonjs/index.d.ts", "default": "./dist/commonjs/index.js" }
                },
                "./decode": {
                  "import": { "types": "./dist/esm/decode.d.ts", "default": "./dist/esm/decode.js" },
                  "require": { "types": "./dist/commonjs/decode.d.ts", "default": "./dist/commonjs/decode.js" }
                }
              }
            }
            """);
        Assert.Equal("dist/esm/decode.js", project.ResolveExportsSubpath("./decode"));
    }

    [Fact]
    public void SkipsRequireAndTypesConditions()
    {
        var project = LoadProject("""
            {
              "exports": {
                "./a": { "types": "./a.d.ts", "require": "./a.cjs", "default": "./a.js" }
              }
            }
            """);
        Assert.Equal("a.js", project.ResolveExportsSubpath("./a"));
    }

    [Theory]
    [InlineData("""{ "browser": "./browser.js", "import": "./esm.js" }""", "browser.js")]
    [InlineData("""{ "import": "./esm.js", "browser": "./browser.js" }""", "esm.js")]
    [InlineData("""{ "node": "./node.js", "module": "./module.js", "default": "./default.js" }""", "module.js")]
    [InlineData("""{ "node": "./node.js", "default": "./default.js" }""", "default.js")]
    [InlineData("""{ "import": { "node": "./node.mjs" }, "default": "./default.js" }""", "default.js")]
    [InlineData("""[ "./first.js", "./second.js" ]""", "first.js")]
    [InlineData("""[ { "require": "./req.cjs" }, "./second.js" ]""", "second.js")]
    [InlineData("\"./plain.js\"", "plain.js")]
    public void UsesFirstMatchingConditionInPackageJsonOrder(string target, string expected)
    {
        var project = LoadProject($$"""{ "exports": { "./sub": {{target}} } }""");
        Assert.Equal(expected, project.ResolveExportsSubpath("./sub"));
    }

    [Theory]
    [InlineData("""{ "exports": { "./decode": "./decode.js" } }""", "./encode")]
    [InlineData("""{ "exports": { "./*": "./dist/*.js" } }""", "./decode")]
    [InlineData("""{ "exports": { "./decode": { "require": "./decode.cjs" } } }""", "./decode")]
    [InlineData("""{ "exports": { "./decode": null } }""", "./decode")]
    [InlineData("""{ "exports": { "import": "./index.js" } }""", "./decode")]
    [InlineData("""{ "exports": "./index.js" }""", "./decode")]
    [InlineData("""{ "main": "index.js" }""", "./decode")]
    public void ReturnsNullForSubpathsNotResolvableThroughExports(string packageJson, string subpath)
    {
        var project = LoadProject(packageJson);
        Assert.Null(project.ResolveExportsSubpath(subpath));
    }
}
