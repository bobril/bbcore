using Markdig.Syntax.Inlines;

namespace BobrilMdx;

public class LinkInlineRenderer : TsxObjectRenderer<LinkInline>
{
    protected override void Write(TsxRenderer renderer, LinkInline link)
    {
        if (renderer.EnableHtmlForInline)
        {
            renderer.Write(link.IsImage ? "<mdx.Img src=" : "<mdx.A href=");
            renderer.WriteEscapeUrl(link.Url, link.IsImage);
            renderer.WriteProps(link);
        }
        if (link.IsImage)
        {
            if (renderer.EnableHtmlForInline)
            {
                renderer.Write(" alt=\"");
            }
            var wasEnableHtmlForInline = renderer.EnableHtmlForInline;
            renderer.EnableHtmlForInline = false;
            renderer.WriteChildren(link);
            renderer.EnableHtmlForInline = wasEnableHtmlForInline;
            if (renderer.EnableHtmlForInline)
            {
                renderer.Write('"');
            }
        }

        if (renderer.EnableHtmlForInline && !string.IsNullOrEmpty(link.Title))
        {
            renderer.Write(" title=\"");
            renderer.WriteEscape(link.Title);
            renderer.Write('"');
        }

        if (link.IsImage)
        {
            if (renderer.EnableHtmlForInline)
            {
                renderer.Write(" />");
            }
        }
        else
        {
            if (renderer.EnableHtmlForInline)
            {
                renderer.Write('>');
            }
            renderer.WriteChildren(link);
            if (renderer.EnableHtmlForInline)
            {
                renderer.Write("</mdx.A>");
            }
        }
    }
}