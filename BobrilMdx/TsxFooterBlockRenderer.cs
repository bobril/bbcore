using Markdig.Extensions.Footers;

namespace BobrilMdx;

public class TsxFooterBlockRenderer : TsxObjectRenderer<FooterBlock>
{
    protected override void Write(TsxRenderer renderer, FooterBlock footer)
    {
        renderer.EnsureLine();
        renderer.Write("<mdx.Footer").WriteProps(footer).Write(">");
        var implicitParagraph = renderer.ImplicitParagraph;
        renderer.ImplicitParagraph = true;
        renderer.WriteChildren(footer);
        renderer.ImplicitParagraph = implicitParagraph;
        renderer.Write("</mdx.Footer>").WriteLine();
    }
}