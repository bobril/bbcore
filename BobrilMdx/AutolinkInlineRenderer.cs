using Markdig.Syntax.Inlines;

namespace BobrilMdx
{
    public class AutolinkInlineRenderer : TsxObjectRenderer<AutolinkInline>
    {
        protected override void Write(TsxRenderer renderer, AutolinkInline obj)
        {
            if (renderer.EnableHtmlForInline)
            {
                renderer.Write("<mdx.A href=");
                renderer.WriteEscapeUrl(obj.IsEmail?  "mailto:"+ obj.Url :obj.Url , false);
                renderer.WriteProps(obj);
                renderer.Write('>');
            }

            renderer.WriteEscape(obj.Url);

            if (renderer.EnableHtmlForInline)
            {
                renderer.Write("</mdx.A>");
            }
        }
    }
}