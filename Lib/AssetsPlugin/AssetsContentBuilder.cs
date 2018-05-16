namespace Lib.AssetsPlugin
{
    public class AssetsContentBuilder : ContentBuilder
    {
        public override string GetHeader() => Notice;

        public override void AddPropertyValue(string value)
        {
            _content.Append('"');
            _content.Append(value);
            _content.Append('"');
        }
    }
}
