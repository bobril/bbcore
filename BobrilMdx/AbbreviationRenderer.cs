using Markdig.Extensions.Abbreviations;

namespace BobrilMdx
{
    public class AbbreviationRenderer : TsxObjectRenderer<AbbreviationInline>
    {
        protected override void Write(TsxRenderer renderer, AbbreviationInline obj)
        {
            // <abbr title="Hyper Text Markup Language">HTML</abbr>
            var abbr = obj.Abbreviation;
            if (renderer.EnableHtmlForInline)
            {
                renderer.Write("<mdx.Abbr").WriteProps(obj).Write(" title=").WriteEscape(ref abbr.Text);
                renderer.Write(">");
            }
            renderer.Write(abbr.Label);
            if (renderer.EnableHtmlForInline)
            {
                renderer.Write("</mdx.Abbr>");
            }
        }
    }
}