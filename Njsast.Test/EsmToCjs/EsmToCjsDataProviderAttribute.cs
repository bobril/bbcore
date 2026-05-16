using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Njsast.Utils;
using Xunit.Sdk;

namespace Test.EsmToCjs;

public class EsmToCjsDataProviderAttribute : DataAttribute
{
    const string CjsFileExtension = ".cjs";
    const string CjsMapFileExtension = ".cjs.map";

    readonly string _testFileDirectory;
    readonly string _searchPattern;
    readonly bool _searchSubDirectories;

    IEnumerable<string> InputFiles => Directory.EnumerateFiles(_testFileDirectory, _searchPattern,
            _searchSubDirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
        .Select(PathUtils.Normalize);

    public EsmToCjsDataProviderAttribute(string testFileDirectory, string searchPattern = "*.js",
        bool searchSubDirectories = true)
    {
        _testFileDirectory = testFileDirectory;
        _searchPattern = searchPattern;
        _searchSubDirectories = searchSubDirectories;
    }

    public IEnumerable<EsmToCjsTestData> GetTypedData()
    {
        return InputFiles.Select(inputFile =>
        {
            var cjsFile = PathUtils.ChangeExtension(inputFile, CjsFileExtension);
            var cjsMapFile = PathUtils.ChangeExtension(inputFile, CjsMapFileExtension);
            return new EsmToCjsTestData(
                name: PathUtils.WithoutExtension(inputFile),
                inputFileName: inputFile,
                inputContent: File.ReadAllText(inputFile),
                expectedCjs: TryReadAllText(cjsFile),
                expectedCjsMap: TryReadAllText(cjsMapFile)
            );
        });
    }

    static string TryReadAllText(string name)
    {
        return File.Exists(name) ? File.ReadAllText(name) : "";
    }

    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {
        return GetTypedData().Select(x => new object[] { x });
    }
}
