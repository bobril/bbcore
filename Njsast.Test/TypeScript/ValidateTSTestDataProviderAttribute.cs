using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Njsast.Utils;
using Xunit.Sdk;

namespace Test.TypeScript;

public sealed class ValidateTSTestDataProviderAttribute : DataAttribute
{
    readonly string _testFileDirectory;

    public ValidateTSTestDataProviderAttribute(string testFileDirectory)
    {
        _testFileDirectory = testFileDirectory;
    }

    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {
        var testFiles = Directory.EnumerateFiles(
            _testFileDirectory, "*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".ts") || f.EndsWith(".tsx"));

        foreach (var inputFile in testFiles)
        {
            var expectedFile = inputFile + ".expected.js";
            if (!File.Exists(expectedFile))
                continue;

            var input = File.ReadAllText(inputFile);
            var expectedJs = File.ReadAllText(expectedFile);

            yield return new object[]
            {
                new ValidateTSTestData
                {
                    Name = inputFile,
                    SourceName = Path.GetFileName(inputFile),
                    Input = input,
                    ExpectedJs = expectedJs
                }
            };
        }
    }
}
