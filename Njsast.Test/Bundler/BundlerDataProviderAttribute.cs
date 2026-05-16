using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Njsast.Utils;
using Xunit.Sdk;

namespace Test.Bundler;

public class BundlerDataProviderAttribute : DataAttribute
{
    readonly string _testFileDirectory;

    IEnumerable<string> Inputs => Directory.EnumerateDirectories(_testFileDirectory)
        .Select(PathUtils.Normalize);

    public BundlerDataProviderAttribute(string testFileDirectory)
    {
        _testFileDirectory = testFileDirectory;
    }

    public IEnumerable<BundlerTestData> GetTypedData()
    {
        return Inputs.Select(inputFile =>
        {
            var allInputFiles = Directory.EnumerateFiles(inputFile, "*.*", SearchOption.AllDirectories)
                .Select(PathUtils.Normalize);

            return new BundlerTestData(allInputFiles.ToDictionary(a => a[(inputFile.Length+1)..], File.ReadAllText), inputFile,
                PathUtils.Name(inputFile));
        });
    }

    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {
        return GetTypedData().Select(x => new object[] {x});
    }
}
