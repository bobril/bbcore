namespace Test.EsmToCjs;

public class EsmToCjsTestData
{
    public string Name;
    public string InputFileName;
    public string InputContent;
    public string ExpectedCjs;
    public string ExpectedCjsMap;

    public EsmToCjsTestData(string name, string inputFileName, string inputContent,
        string expectedCjs, string expectedCjsMap)
    {
        Name = name;
        InputFileName = inputFileName;
        InputContent = inputContent;
        ExpectedCjs = expectedCjs;
        ExpectedCjsMap = expectedCjsMap;
    }
}
