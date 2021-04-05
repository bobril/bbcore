using Markdig.Syntax.Inlines;

namespace BobrilMdx
{
    public class HtmlEntityInlineRenderer : TsxObjectRenderer<HtmlEntityInline>
    {
        protected override void Write(TsxRenderer renderer, HtmlEntityInline obj)
        {
            if (renderer.EnableHtmlEscape)
            {
                var slice = obj.Transcoded;
                renderer.WriteEscape(ref slice);
            }
            else
            {
                renderer.Write(obj.Transcoded);
            }
        }
    }
}