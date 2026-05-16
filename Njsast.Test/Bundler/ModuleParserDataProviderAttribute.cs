using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Njsast.Utils;
using Test.ConstEval;
using Xunit.Sdk;

namespace Test.Bundler;

public class ModuleParserDataProviderAttribute : DataAttribute
{
    const string NiceJsFileExtension = ".nicejs";

    readonly string _testFileDirectory;
    readonly string _searchPattern;
    readonly bool _searchSubDirectories;

    IEnumerable<string> InputFiles => Directory.EnumerateFiles(_testFileDirectory, _searchPattern,
            _searchSubDirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
        .Select(PathUtils.Normalize);

    public ModuleParserDataProviderAttribute(string testFileDirectory, string searchPattern = "*.js",
        bool searchSubDirectories = true)
    {
        _testFileDirectory = testFileDirectory;
        _searchPattern = searchPattern;
        _searchSubDirectories = searchSubDirectories;
    }

    public IEnumerable<ConstEvalTestData> GetTypedData()
    {
        return InputFiles.Select(inputFile =>
        {
            var niceJsFile = PathUtils.ChangeExtension(inputFile, NiceJsFileExtension);
            return new ConstEvalTestData
            {
                InputContent = File.ReadAllText(inputFile),
                InputFileName = inputFile,
                Name = PathUtils.WithoutExtension(inputFile),
                ExpectedNiceJs = TryReadAllText(niceJsFile)
            };
        });
    }

    static string TryReadAllText(string name)
    {
        return File.Exists(name) ? File.ReadAllText(name) : "";
    }

    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {
        return GetTypedData().Select(x => new object[] {x});
    }
}