namespace Lib.CSSProcessor;

public struct SourceFromPair
{
    public SourceFromPair(string source, string from)
    {
        Source = source;
        From = from;
    }
    public string Source;
    public string From;
}