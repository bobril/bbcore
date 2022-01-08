using Markdig.Extensions.DefinitionLists;
using Markdig.Syntax;

namespace BobrilMdx;

public class TsxDefinitionListRenderer : TsxObjectRenderer<DefinitionList>
{
    protected override void Write(TsxRenderer renderer, DefinitionList list)
    {
        renderer.EnsureLine();
        renderer.Write("<mdx.Dl").WriteProps(list).Write('>').WriteLine();
        foreach (var item in list)
        {
            var hasOpendd = false;
            var definitionItem = (DefinitionItem) item;
            var countdd = 0;
            var lastWasSimpleParagraph = false;
            for (var i = 0; i < definitionItem.Count; i++)
            {
                var definitionTermOrContent = definitionItem[i];
                if (definitionTermOrContent is DefinitionTerm definitionTerm)
                {
                    if (hasOpendd)
                    {
                        if (!lastWasSimpleParagraph)
                        {
                            renderer.EnsureLine();
                        }
                        renderer.Write("</mdx.Dd>").WriteLine();
                        lastWasSimpleParagraph = false;
                        hasOpendd = false;
                        countdd = 0;
                    }
                    renderer.Write("<mdx.Dt").WriteProps(definitionTerm).Write('>');
                    renderer.WriteLeafInline(definitionTerm);
                    renderer.Write("</mdx.Dt>").WriteLine();
                }
                else
                {
                    if (!hasOpendd)
                    {
                        renderer.Write("<mdx.Dd").WriteProps(definitionItem).Write('>');
                        countdd = 0;
                        hasOpendd = true;
                    }

                    var nextTerm = i + 1 < definitionItem.Count ? definitionItem[i + 1] : null;
                    var isSimpleParagraph = (nextTerm is null or DefinitionItem) && countdd == 0 &&
                                            definitionTermOrContent is ParagraphBlock;

                    var saveImplicitParagraph = renderer.ImplicitParagraph;
                    if (isSimpleParagraph)
                    {
                        renderer.ImplicitParagraph = true;
                        lastWasSimpleParagraph = true;
                    }
                    renderer.Write(definitionTermOrContent);
                    renderer.ImplicitParagraph = saveImplicitParagraph;
                    countdd++;
                }
            }
            if (hasOpendd)
            {
                if (!lastWasSimpleParagraph)
                {
                    renderer.EnsureLine();
                }
                renderer.Write("</mdx.Dd>").WriteLine();
            }
        }
        renderer.EnsureLine();
        renderer.Write("</mdx.Dl>").WriteLine();
    }
}