using Markdig.Syntax;

namespace BobrilMdx;

public class ThematicBreakRenderer : TsxObjectRenderer<ThematicBreakBlock>
{
    protected override void Write(TsxRenderer renderer, ThematicBreakBlock obj)
    {
        if (renderer.EnableHtmlForBlock)
        {
            renderer.Write("<mdx.Hr").WriteProps(obj).Write(" />").WriteLine();
        }
    }
}