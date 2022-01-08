namespace Lib.AssetsPlugin;

public class AssetsContentBuilder : ContentBuilder
{
    protected override string GetHeader() => Notice;

    protected override void AddPropertyValue(string value)
    {
        ContentStringBuilder!.Append('"');
        ContentStringBuilder.Append(value);
        ContentStringBuilder.Append('"');
    }
}