using System.Reflection.Metadata;
using Markdig.Syntax.Inlines;

namespace BobrilMdx;

public class LiteralInlineRenderer : TsxObjectRenderer<LiteralInline>
{
    protected override void Write(TsxRenderer renderer, LiteralInline obj)
    {
        if (renderer.EnableHtmlEscape && renderer.EnableHtmlForInline)
        {
            renderer.WriteEscape(ref obj.Content);
        }
        else
        {
            renderer.Write(ref obj.Content);
        }
    }
}