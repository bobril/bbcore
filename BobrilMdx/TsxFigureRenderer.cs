using Markdig.Extensions.Figures;

namespace BobrilMdx
{
    public class TsxFigureRenderer : TsxObjectRenderer<Figure>
    {
        protected override void Write(TsxRenderer renderer, Figure obj)
        {
            renderer.EnsureLine();
            renderer.Write("<mdx.Figure").WriteProps(obj).Write(">").WriteLine();
            renderer.WriteChildren(obj);
            renderer.Write("</mdx.Figure>").WriteLine();
        }
    }
}