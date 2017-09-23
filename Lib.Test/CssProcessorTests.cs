using Lib.Utils;
using System;
using Xunit;

namespace Lib.Test
{
    public class CssProcessorTests
    {
        string _bbdir;
        ToolsDir.ToolsDir _tools;

        public CssProcessorTests()
        {
            _bbdir = PathUtils.Join(PathUtils.Normalize(Environment.CurrentDirectory), ".bbcore");
            _tools = new ToolsDir.ToolsDir(PathUtils.Join(_bbdir, "tools"));
        }

        [Fact]
        void ProcessSimpleCss()
        {
            var cssProcessor = new CSSProcessor.CssProcessor(_tools);
            Assert.Equal(".c { width: 100% }", cssProcessor.ProcessCss(".c { width: 100% }", "/dir/file.css", (url, from) => url).Result);
        }

        [Fact]
        void ProcessCssWithUrl()
        {
            var cssProcessor = new CSSProcessor.CssProcessor(_tools);
            Func<string, string, string> urlReplacerUrlFrom = (url, from) =>
                     {
                         Assert.Equal("logo.png", url);
                         Assert.Equal("./dir", from);
                         return from + "/" + url;
                     };
            Assert.Equal(".c { background-image: url(\"./dir/logo.png\") }", 
                cssProcessor.ProcessCss(".c { background-image: url(\"logo.png\") }", "./dir/file.css",
                urlReplacerUrlFrom).Result);
        }
    }
}
