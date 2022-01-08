using Markdig.Syntax.Inlines;

namespace BobrilMdx;

public class DelimiterInlineRenderer : TsxObjectRenderer<DelimiterInline>
{
    protected override void Write(TsxRenderer renderer, DelimiterInline obj)
    {
        renderer.WriteEscape(obj.ToLiteral());
        renderer.WriteChildren(obj);
    }
}