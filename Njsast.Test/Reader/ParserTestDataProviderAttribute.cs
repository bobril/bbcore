using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Njsast.Reader;
using Njsast.Utils;
using Xunit.Sdk;

namespace Test.Reader;

public class ParserTestDataProviderAttribute : DataAttribute
{
    const string InputFileExtension = ".js";
    const string InputMapFileExtension = ".js.map";
    const string OutputFileExtension = ".txt";
    const string NiceJsFileExtension = ".nicejs";
    const string NiceJsMapFileExtension = ".nicejs.map";
    const string MinJsFileExtension = ".minjs";
    const string MinJsMapFileExtension = ".minjs.map";

    readonly Regex _ecmaScriptVersion = new Regex("es([0-9]+)");
    readonly string _testFileDirectory;
    readonly string _searchPattern;
    readonly bool _searchSubDirectories;

    IEnumerable<string> InputFiles =>
        Directory
            .EnumerateFiles(_testFileDirectory, _searchPattern,
                _searchSubDirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
            .Where(x => x.EndsWith(InputFileExtension)).Select(PathUtils.Normalize);

    public ParserTestDataProviderAttribute(string testFileDirectory, string searchPattern = "*",
        bool searchSubDirectories = true)
    {
        _testFileDirectory = testFileDirectory;
        _searchPattern = searchPattern;
        _searchSubDirectories = searchSubDirectories;
    }

    public IEnumerable<ParserTestData> GetParserData()
    {
        return InputFiles.Select(inputFile =>
        {
            var inputMapFile = PathUtils.ChangeExtension(inputFile, InputMapFileExtension);
            var outputFile = PathUtils.ChangeExtension(inputFile, OutputFileExtension);
            var niceJsFile = PathUtils.ChangeExtension(inputFile, NiceJsFileExtension);
            var niceJsMapFile = PathUtils.ChangeExtension(inputFile, NiceJsMapFileExtension);
            var minJsFile = PathUtils.ChangeExtension(inputFile, MinJsFileExtension);
            var minJsMapFile = PathUtils.ChangeExtension(inputFile, MinJsMapFileExtension);
            var isInvalid = !File.Exists(niceJsFile) || !File.Exists(minJsFile);

            var testData = new ParserTestData
            {
                Name = PathUtils.WithoutExtension(inputFile),
                Input = File.ReadAllText(inputFile),
                SourceName = PathUtils.Name(inputFile),
                ExpectedAst = File.Exists(outputFile) ? File.ReadAllText(outputFile) : "",
                IsInvalid = isInvalid,
                EcmaScriptVersion = GetEcmaVersion(inputFile)
            };
            if (File.Exists(inputMapFile))
            {
                testData.InputSourceMap = File.ReadAllText(inputMapFile);
            }

            if (isInvalid) return testData;
            testData.ExpectedMinJs = File.ReadAllText(minJsFile);
            testData.ExpectedMinJsMap = File.ReadAllText(minJsMapFile);
            testData.ExpectedNiceJs = File.ReadAllText(niceJsFile);
            testData.ExpectedNiceJsMap = File.ReadAllText(niceJsMapFile);

            return testData;
        });
    }

    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {
        return GetParserData().Select(x => new object[] {x});
    }

    int GetEcmaVersion(string fileName)
    {
        var match = _ecmaScriptVersion.Match(fileName);
        if (!match.Success)
        {
            return Options.DefaultEcmaVersion;
        }

        return int.Parse(match.Groups[1].Value);
    }
}