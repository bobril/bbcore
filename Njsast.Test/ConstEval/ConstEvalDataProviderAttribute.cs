using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Njsast.Utils;
using Xunit.Sdk;

namespace Test.ConstEval;

public class ConstEvalDataProviderAttribute : DataAttribute
{
    const string NiceJsFileExtension = ".nicejs";

    readonly string _testFileDirectory;
    readonly string _searchPattern;
    readonly bool _searchSubDirectories;

    IEnumerable<string> InputFiles => Directory.EnumerateFiles(_testFileDirectory, _searchPattern,
            _searchSubDirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
        .Select(PathUtils.Normalize).Where(x => !PathUtils.Name(x).StartsWith("dep-", StringComparison.Ordinal));

    public ConstEvalDataProviderAttribute(string testFileDirectory, string searchPattern = "*.js",
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
                ExpectedNiceJs = File.ReadAllText(niceJsFile)
            };
        });
    }

    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {
        return GetTypedData().Select(x => new object[] {x});
    }
}