using Markdig.Syntax;

namespace BobrilMdx;

public class QuoteBlockRenderer : TsxObjectRenderer<QuoteBlock>
{
    protected override void Write(TsxRenderer renderer, QuoteBlock obj)
    {
        renderer.EnsureLine();
        if (renderer.EnableHtmlForBlock)
        {
            renderer.Write("<mdx.BlockQuote").WriteProps(obj).Write(">").WriteLine();
        }
        var savedImplicitParagraph = renderer.ImplicitParagraph;
        renderer.ImplicitParagraph = false;
        renderer.WriteChildren(obj);
        renderer.ImplicitParagraph = savedImplicitParagraph;
        if (renderer.EnableHtmlForBlock)
        {
            renderer.Write("</mdx.BlockQuote>").WriteLine();
        }
        renderer.EnsureLine();
    }
}