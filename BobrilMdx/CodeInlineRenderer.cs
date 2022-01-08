using Markdig.Syntax.Inlines;

namespace BobrilMdx;

public class CodeInlineRenderer : TsxObjectRenderer<CodeInline>
{
    protected override void Write(TsxRenderer renderer, CodeInline obj)
    {
        if (renderer.EnableHtmlForInline)
        {
            renderer.Write("<mdx.Code").WriteProps(obj).Write('>');
        }
        if (renderer.EnableHtmlEscape)
        {
            renderer.WriteEscape(obj.Content);
        }
        else
        {
            renderer.Write(obj.Content);
        }
        if (renderer.EnableHtmlForInline)
        {
            renderer.Write("</mdx.Code>");
        }
    }
}