using Lib.Utils;
using System;

namespace Lib.AssetsPlugin
{
    public class SpritesContentBuilder : ContentBuilder
    {
        const string _importBobril = "import * as b from 'bobril';\n";

        public override string GetHeader() => Notice + _importBobril;

        public override bool ShouldSkip(string value)
        {
            return !PathUtils.GetExtension(value).SequenceEqual("png");
        }

        public override void AddPropertyValue(string value)
        {
            _content.Append("b.sprite(\"");
            _content.Append(value);
            _content.Append("\")");
        }
    }
}
