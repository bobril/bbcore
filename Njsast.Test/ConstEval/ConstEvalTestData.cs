namespace Test.ConstEval;

public class ConstEvalTestData
{
    public string Name { get; set; } = string.Empty;
    public string InputFileName { get; set; } = string.Empty;
    public string InputContent { get; set; } = string.Empty;
    public string ExpectedNiceJs { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"Name: {Name}";
    }
}