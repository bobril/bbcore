using System;
using Markdig.Parsers;
using Markdig.Syntax;

namespace BobrilMdx
{
    public class CodeBlockRenderer : TsxObjectRenderer<CodeBlock>
    {
        protected override void Write(TsxRenderer renderer, CodeBlock obj)
        {
            renderer.EnsureLine();

            var fencedCodeBlock = obj as FencedCodeBlock;
            if (renderer.EnableHtmlForBlock)
            {
                renderer.Write("<mdx.CodeBlock");
                if (fencedCodeBlock?.Info != null)
                    renderer.Write(" info=").WriteJsString(fencedCodeBlock.Info);
                if (fencedCodeBlock?.Arguments != null)
                    renderer.Write(" args=").WriteJsString(fencedCodeBlock.Arguments);
                renderer.WriteProps(obj).Write('>');
            }

            renderer.WriteLeafRawLines(obj, false, true);

            if (renderer.EnableHtmlForBlock)
            {
                renderer.Write("</mdx.CodeBlock>").WriteLine();
            }

            renderer.EnsureLine();
        }
    }
}
