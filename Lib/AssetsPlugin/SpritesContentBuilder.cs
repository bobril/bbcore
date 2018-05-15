using Lib.Utils;

namespace Lib.AssetsPlugin
{
    public class SpritesContentBuilder : ContentBuilder
    {
        const string _importBobril = "import * as b from 'bobril';\n";

        public override string GetHeader() => Notice + _importBobril + "\n";

        public override void AddPropertyValue(string name, string value, int depth)
        {
            if (PathUtils.GetExtension(value) != "png") return;
            var sanitizedName = SanitizePropertyName(name);
            _content += sanitizedName + GetPropertyNameValueSeparator(depth) + "b.sprite(\"" + value + "\")";
            _content += GetPropertyLineEnd(depth);
        }
    }
}
