using Markdig.Syntax.Inlines;

namespace BobrilMdx
{
    public class HtmlInlineRenderer : TsxObjectRenderer<HtmlInline>
    {
        protected override void Write(TsxRenderer renderer, HtmlInline obj)
        {
            if (renderer.EnableHtmlForInline)
            {
                renderer.Write(obj.Tag);
            }
        }
    }
}