using Markdig.Syntax;

namespace BobrilMdx;

public class ParagraphRenderer : TsxObjectRenderer<ParagraphBlock>
{
    protected override void Write(TsxRenderer renderer, ParagraphBlock obj)
    {
        if (!renderer.ImplicitParagraph && renderer.EnableHtmlForBlock)
        {
            if (!renderer.IsFirstInContainer)
            {
                renderer.EnsureLine();
            }

            renderer.Write("<mdx.P").WriteProps(obj).Write(">").Indent();
        }
        renderer.WriteLeafInline(obj);
        if (!renderer.ImplicitParagraph)
        {
            if(renderer.EnableHtmlForBlock)
            {
                renderer.Dedent();
                renderer.Write("</mdx.P>");
            }

            renderer.EnsureLine();
        }
    }
}