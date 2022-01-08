using Markdig.Syntax;

namespace BobrilMdx;

public class HtmlBlockRenderer : TsxObjectRenderer<HtmlBlock>
{
    protected override void Write(TsxRenderer renderer, HtmlBlock obj)
    {
        renderer.WriteLeafRawLines(obj, true, false);
    }
}