using System.Collections.Generic;

namespace Test.SourceInfo;

public class BobrilSourceInfoTestData
{
    public BobrilSourceInfoTestData(Dictionary<string, string> inputContent, string input, string name)
    {
        InputContent = inputContent;
        Input = input;
        Name = name;
    }

    public Dictionary<string, string> InputContent;
    public string Input;
    public string Name;
}