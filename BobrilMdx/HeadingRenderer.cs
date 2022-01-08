using Markdig.Syntax;

namespace BobrilMdx;

public class HeadingRenderer : TsxObjectRenderer<HeadingBlock>
{
    protected override void Write(TsxRenderer renderer, HeadingBlock obj)
    {
        if (renderer.EnableHtmlForBlock)
        {
            renderer.Write("<mdx.H level={"+obj.Level+"}").WriteProps(obj).Write('>');
        }

        renderer.WriteLeafInline(obj);

        if (renderer.EnableHtmlForBlock)
        {
            renderer.Write("</mdx.H>").WriteLine();
        }

        renderer.EnsureLine();
    }
}