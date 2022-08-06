namespace Njsast.SourceMap;

public class SourceCodePosition
{
    public string SourceName = string.Empty;
    public int Line;
    public int Col;

    public override string ToString()
    {
        return SourceName + ":" + Line + ":" + Col;
    }
}
