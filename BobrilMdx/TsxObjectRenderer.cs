using Markdig.Renderers;
using Markdig.Syntax;

namespace BobrilMdx
{
    public abstract class TsxObjectRenderer<TObject> : MarkdownObjectRenderer<TsxRenderer, TObject> where TObject : MarkdownObject
    {
    }
}