using Lib.Utils;
using System;
using Shared.Utils;

namespace Lib.AssetsPlugin;

public class SpritesContentBuilder : ContentBuilder
{
    const string ImportBobril = "import * as b from 'bobril';\n";

    protected override string GetHeader() => Notice + ImportBobril;

    protected override bool ShouldSkip(string value)
    {
        return !PathUtils.GetExtension(value).SequenceEqual("png") && !PathUtils.GetExtension(value).SequenceEqual("svg");
    }

    protected override void AddPropertyValue(string value)
    {
        ContentStringBuilder!.Append("b.sprite(\"");
        ContentStringBuilder.Append(value);
        ContentStringBuilder.Append("\")");
    }
}