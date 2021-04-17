using Markdig.Extensions.Figures;

namespace BobrilMdx
{
    public class TsxFigureCaptionRenderer : TsxObjectRenderer<FigureCaption>
    {
        protected override void Write(TsxRenderer renderer, FigureCaption obj)
        {
            renderer.EnsureLine();
            renderer.Write("<mdx.Figcaption").WriteProps(obj).Write('>');
            renderer.WriteLeafInline(obj);
            renderer.Write("</mdx.Figcaption>").WriteLine();
        }
    }
}