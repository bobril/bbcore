using Markdig.Syntax;

namespace BobrilMdx
{
    public class ListRenderer : TsxObjectRenderer<ListBlock>
    {
        protected override void Write(TsxRenderer renderer, ListBlock listBlock)
        {
            renderer.EnsureLine();
            if (renderer.EnableHtmlForBlock)
            {
                if (listBlock.IsOrdered)
                {
                    renderer.Write("<mdx.Ol");
                    if (listBlock.BulletType != '1')
                    {
                        renderer.Write(" type=\"").Write(listBlock.BulletType).Write('"');
                    }

                    if (listBlock.OrderedStart is { } and not "1")
                    {
                        renderer.Write(" start={").Write(listBlock.OrderedStart).Write('}');
                    }
                    renderer.WriteProps(listBlock);
                    renderer.Write('>').WriteLine().Indent();
                }
                else
                {
                    renderer.Write("<mdx.Ul");
                    renderer.WriteProps(listBlock);
                    renderer.Write('>').WriteLine().Indent();
                }
            }

            foreach (var item in listBlock)
            {
                var listItem = (ListItemBlock)item;
                var previousImplicit = renderer.ImplicitParagraph;
                renderer.ImplicitParagraph = !listBlock.IsLoose;

                renderer.EnsureLine();
                if (renderer.EnableHtmlForBlock)
                {
                    renderer.Write("<mdx.Li").WriteProps(listItem).Write('>');
                }

                renderer.WriteChildren(listItem);

                if (renderer.EnableHtmlForBlock)
                {
                    renderer.Write("</mdx.Li>").WriteLine();
                }

                renderer.EnsureLine();
                renderer.ImplicitParagraph = previousImplicit;
            }

            if (renderer.EnableHtmlForBlock)
            {
                renderer.Dedent().Write(listBlock.IsOrdered ? "</mdx.Ol>" : "</mdx.Ul>").WriteLine();
            }

            renderer.EnsureLine();
        }
    }
}