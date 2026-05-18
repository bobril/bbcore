using Lib.CSSProcessor;

namespace Bbcore.Lib.Test;

public class CssProcessorNativeTests
{
    [Fact]
    public async Task NativeCssProcessorConcatenatesMinifiesAndRewritesUrls()
    {
        var old = Environment.GetEnvironmentVariable("BBCSS");
        Environment.SetEnvironmentVariable("BBCSS", "native");
        try
        {
            using var processor = new CssProcessor(null!);
            var result = await processor.ConcatenateAndMinifyCss(new[]
            {
                new SourceFromPair(".a{background:url(img/a.png)}", "/src/a.css"),
                new SourceFromPair(".b{mask:url(data:image/png;base64,abc)}", "/src/b.css")
            }, (url, from) => from + ":" + url);

            Assert.Equal(".a{background:url(/src/a.css:img/a.png);}.b{mask:url(data:image/png;base64,abc);}", result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BBCSS", old);
        }
    }

    [Fact]
    public async Task NativeCssProcessorInlinesRelativeAndFileImportsThroughCallback()
    {
        var old = Environment.GetEnvironmentVariable("BBCSS");
        Environment.SetEnvironmentVariable("BBCSS", "native");
        try
        {
            using var processor = new CssProcessor(null!);
            var loaded = new List<(string Url, string From)>();
            var result = await processor.ProcessCss(
                "@import \"dep.css\";@import url(\"file:///src/file.css\");@import url(\"https://example.com/x.css\");.a{color:red}",
                "/src/main.css",
                (url, from) => url,
                (url, from) =>
                {
                    loaded.Add((url, from));
                    return url switch
                    {
                        "dep.css" => new SourceFromPair(".dep{color:blue}", "/src/dep.css"),
                        "file:///src/file.css" => new SourceFromPair(".file{color:green}", "/src/file.css"),
                        _ => null
                    };
                });

            Assert.Equal("""
                .dep {
                    color: blue;
                }
                .file {
                    color: green;
                }
                @import url("https://example.com/x.css");
                .a {
                    color: red;
                }

                """.Replace("\r\n", "\n"), result);
            Assert.Equal(new[] { ("dep.css", "/src/main.css"), ("file:///src/file.css", "/src/main.css") }, loaded);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BBCSS", old);
        }
    }

    [Fact]
    public async Task NativeCssProcessorRunsAstMinifierForConcatenation()
    {
        var old = Environment.GetEnvironmentVariable("BBCSS");
        Environment.SetEnvironmentVariable("BBCSS", "native");
        try
        {
            using var processor = new CssProcessor(null!);
            var result = await processor.ConcatenateAndMinifyCss(new[]
            {
                new SourceFromPair(".a > .b { margin: 0px 1.5000px; color: #aabbcc; background: white }", "/src/a.css")
            }, (url, from) => url);

            Assert.Equal(".a>.b{margin:0 1.5px;color:#abc;background:#fff;}", result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BBCSS", old);
        }
    }
}
