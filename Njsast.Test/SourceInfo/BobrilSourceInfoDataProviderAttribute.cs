using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Njsast.Utils;
using Xunit.Sdk;

namespace Test.SourceInfo;

public class BobrilSourceInfoDataProviderAttribute : DataAttribute
{
    readonly string _testFileDirectory;

    IEnumerable<string> Inputs => Directory.EnumerateDirectories(_testFileDirectory)
        .Select(PathUtils.Normalize);

    public BobrilSourceInfoDataProviderAttribute(string testFileDirectory)
    {
        _testFileDirectory = testFileDirectory;
    }

    public IEnumerable<BobrilSourceInfoTestData> GetTypedData()
    {
        return Inputs.Select(inputFile =>
        {
            var allInputFiles = Directory.EnumerateFiles(inputFile, "*.*", SearchOption.AllDirectories)
                .Select(PathUtils.Normalize);

            return new BobrilSourceInfoTestData(allInputFiles.ToDictionary(a => a.Substring(inputFile.Length+1), File.ReadAllText), inputFile,
                PathUtils.Name(inputFile));
        });
    }

    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {
        return GetTypedData().Select(x => new object[] {x});
    }
}