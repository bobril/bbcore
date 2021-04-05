using Markdig.Syntax.Inlines;

namespace BobrilMdx
{
    public class LineBreakInlineRenderer : TsxObjectRenderer<LineBreakInline>
    {
        protected override void Write(TsxRenderer renderer, LineBreakInline obj)
        {
            if (renderer.EnableHtmlForInline)
            {
                if (obj.IsHard)
                {
                    renderer.Write("{mdx.Br()}");
                }
            }

            renderer.EnsureLine();
        }
    }
}
