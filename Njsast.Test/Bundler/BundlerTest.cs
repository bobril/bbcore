using System;
using System.Collections.Generic;
using System.Linq;
using Njsast.Ast;
using Njsast.Bundler;
using Njsast.Compress;
using Njsast.Output;
using Njsast.SourceMap;
using Njsast.Utils;
using Xunit;

namespace Test.Bundler;

public class BundlerTest
{
    [Theory]
    [BundlerDataProvider("Input/Bundler")]
    public void ShouldCorrectlyBundle(BundlerTestData testData)
    {
        var outFiles = BundlerTestCore(testData);

        foreach (var (name, genContent) in outFiles)
        {
            var expected = testData.InputContent.TryGetValue("out/" + name, out var content) ? content : "";
            Assert.Equal(expected, genContent);
        }
    }

    public static Dictionary<string, string> BundlerTestCore(BundlerTestData testData)
    {
        var output = new Dictionary<string, string>();
        var bundler = new BundlerImpl(new BundlerCtx(testData, output, "cbm-"));
        bundler.Mangle = true;
        bundler.CompressOptions = CompressOptions.Default;
        bundler.OutputOptions = new() {Beautify = true};
        InitCommonParts(testData);
        bundler.Run();

        bundler = new(new BundlerCtx(testData, output, "cm-"));
        bundler.Mangle = true;
        bundler.CompressOptions = CompressOptions.Default;
        bundler.OutputOptions = new() {Beautify = false};
        InitCommonParts(testData);
        bundler.Run();

        bundler = new(new BundlerCtx(testData, output, "cb-"));
        bundler.Mangle = false;
        bundler.CompressOptions = CompressOptions.Default;
        bundler.OutputOptions = new() {Beautify = true};
        InitCommonParts(testData);
        bundler.Run();

        bundler = new(new BundlerCtx(testData, output, "b-"));
        bundler.Mangle = false;
        bundler.CompressOptions = null;
        bundler.OutputOptions = new() {Beautify = true};
        InitCommonParts(testData);
        bundler.Run();

        return output;

        void InitCommonParts(BundlerTestData testData)
        {
            var libraryMode = testData.Name.StartsWith("Library");
            bundler.LibraryMode = libraryMode;
            bundler.PartToMainFilesMap =
                new Dictionary<string, IReadOnlyList<string>> {{"bundle", new[] {"index.js"}}};
            bundler.GlobalDefines = new Dictionary<string, object> {{"DEBUG", false}};
            bundler.OutputOptions.Ecma = libraryMode ? 10 : 6;
        }
    }

    public class BundlerCtx : IBundlerCtx
    {
        readonly BundlerTestData _testData;
        readonly Dictionary<string, string> _output;
        readonly string _outputPrefix;

        public BundlerCtx(BundlerTestData testData, Dictionary<string, string> output, string outputPrefix)
        {
            _testData = testData;
            _output = output;
            _outputPrefix = outputPrefix;
        }

        (string?, SourceMap?) IBundlerCtx.ReadContent(string fileName)
        {
            _testData.InputContent.TryGetValue(fileName, out var res);
            return (res, null);
        }

        public IEnumerable<string> GetPlainJsDependencies(string fileName)
        {
            var dir = PathUtils.WithoutExtension(fileName) + "/";
            return _testData.InputContent.Keys.Where(n => n.StartsWith(dir, StringComparison.Ordinal)).ToArray();
        }

        public string GenerateBundleName(string forName)
        {
            return _outputPrefix + PathUtils.ChangeExtension(forName, ".js");
        }

        public string ResolveRequire(string name, string @from)
        {
            if (name.Contains("External")) return IBundlerCtx.LeaveAsExternal;
            var res = PathUtils.Join(PathUtils.Parent("./" + from), name);
            if (res.StartsWith("./", StringComparison.Ordinal)) res = res[2..];
            if (res.EndsWith(".json", StringComparison.Ordinal)) return res;
            return res + ".js";
        }

        public string JsHeaders(string forSplit, bool withImport)
        {
            return BundlerHelpers.JsHeaders(withImport);
        }

        public void WriteBundle(string name, string content)
        {
            _output[name] = content;
        }

        public void WriteBundle(string name, SourceMapBuilder content)
        {
            throw new InvalidOperationException();
        }

        public void ReportTime(string name, TimeSpan duration)
        {
        }

        public void ModifyBundle(string name, AstToplevel topLevelAst)
        {
        }
    }
}
