using System.Collections.Generic;

namespace Test.Bundler;

public class BundlerTestData
{
    public BundlerTestData(Dictionary<string, string> inputContent, string input, string name)
    {
        InputContent = inputContent;
        Input = input;
        Name = name;
    }

    public Dictionary<string, string> InputContent;
    public string Input;
    public string Name;
}