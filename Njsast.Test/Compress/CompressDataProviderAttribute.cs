using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Njsast.Utils;
using Xunit.Sdk;

namespace Test.Compress;

public class CompressDataProviderAttribute : DataAttribute
{
    const string NiceJsFileExtension = ".nicejs";
    const string MinJsFileExtension = ".minjs";
    const string AstFileExtension = ".txt";
    readonly string _testFileDirectory;
    readonly string _searchPattern;
    readonly bool _searchSubDirectories;

    IEnumerable<string> InputFiles => Directory.EnumerateFiles(_testFileDirectory, _searchPattern,
            _searchSubDirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
        .Select(PathUtils.Normalize);

    public CompressDataProviderAttribute(string testFileDirectory, string searchPattern = "*.js",
        bool searchSubDirectories = true)
    {
        _testFileDirectory = testFileDirectory;
        _searchPattern = searchPattern;
        _searchSubDirectories = searchSubDirectories;
    }

    public IEnumerable<CompressTestData> GetTypedData()
    {
        return InputFiles.Select(inputFile =>
        {
            var astFile = PathUtils.ChangeExtension(inputFile, AstFileExtension);
            var niceJsFile = PathUtils.ChangeExtension(inputFile, NiceJsFileExtension);
            var minJsFile = PathUtils.ChangeExtension(inputFile, MinJsFileExtension);
            return new CompressTestData
            {
                InputFileName = inputFile,
                InputContent = File.ReadAllText(inputFile),
                Name = PathUtils.WithoutExtension(inputFile),
                ExpectedAst = TryReadAllText(astFile),
                ExpectedNiceJs = TryReadAllText(niceJsFile),
                ExpectedMinJs = TryReadAllText(minJsFile)
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