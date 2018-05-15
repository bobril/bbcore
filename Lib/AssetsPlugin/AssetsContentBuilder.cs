namespace Lib.AssetsPlugin
{
    public class AssetsContentBuilder : ContentBuilder
    {
        public override string GetHeader() => Notice + "\n";

        public override void AddPropertyValue(string name, string value, int depth)
        {
            var sanitizedName = SanitizePropertyName(name);
            _content += sanitizedName + GetPropertyNameValueSeparator(depth) + "\"" + value + "\"";
            _content += GetPropertyLineEnd(depth);
        }
    }
}
