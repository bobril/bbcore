namespace Test.TypeScript;

public sealed class TypeScriptParserTestData
{
    public string Name { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public string Input { get; set; } = string.Empty;
    public string ExpectedAst { get; set; } = string.Empty;
    public string ExpectedNiceJs { get; set; } = string.Empty;
    public string? ExpectedNiceJsMap { get; set; }
    public string ExpectedMinJs { get; set; } = string.Empty;
    public string? ExpectedMinJsMap { get; set; }

    public override string ToString()
    {
        return $"Name: {Name}";
    }
}
