using Lib.DiskCache;

namespace Bbcore.Lib.Test;

public class BuildTest
{
    [Fact]
    public void BuildWorks()
    {
        var lib = new BbcoreLibrary();
        var files = new InMemoryFs();
        files.WriteAllUtf8("/tmp/index.ts", "let a: string = 'ahoj'; console.log(a);");
        var result = lib.RunBuild(files, "5.2.2", ".", out var javaScript, out var parsedMessages);
        Assert.True(result);
        Assert.Equal("let a_index = \"ahoj\";\n\nconsole.log(a_index);\n\n", javaScript);
        Assert.Null(parsedMessages);
    }
    
    [Fact]
    public void BuildWithSourceMapWorks()
    {
        var lib = new BbcoreLibrary();
        var files = new InMemoryFs();
        files.WriteAllUtf8("/tmp/index.ts", "let a: string = 'ahoj'; console.log(a);");
        var result = lib.RunBuild(files, "5.2.2", ".", out var javaScript, out var parsedMessages, out var sourceMap);
        Assert.True(result);
        Assert.Equal("let a_index = \"ahoj\";\n\nconsole.log(a_index);\n\n//# sourceMappingURL=a.js.map\n", javaScript);
        Assert.Null(parsedMessages);
        Assert.Equal("{\"version\":3,\"sourceRoot\":\".\",\"sources\":[\"tmp/index.ts\"],\"mappings\":\"AAAA,cAAgB;;AAAQ,OAAO;;;\"}", sourceMap);
    }
}