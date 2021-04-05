using System;
using Markdig.Syntax.Inlines;

namespace BobrilMdx
{
    public class EmphasisInlineRenderer : TsxObjectRenderer<EmphasisInline>
    {
        protected override void Write(TsxRenderer renderer, EmphasisInline obj)
        {
            string? tag = null;
            if (renderer.EnableHtmlForInline)
            {
                var c = obj.DelimiterChar;
                tag = c switch
                {
                    '~' => obj.DelimiterCount == 2 ? "Del" : "Sub",
                    '^' => "Sup",
                    '+' => "Ins",
                    '=' => "Mark",
                    '*' or '_' => obj.DelimiterCount == 2 ? "Strong" : "Em",
                    _ => throw new NotSupportedException($"Delimiter: {c}")
                };
                renderer.Write("<mdx.").Write(tag).WriteProps(obj).Write('>');
            }
            renderer.WriteChildren(obj);
            if (renderer.EnableHtmlForInline)
            {
                renderer.Write("</mdx.").Write(tag).Write('>');
            }
        }
    }
}
