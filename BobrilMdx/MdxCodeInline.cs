using System.Diagnostics;
using Markdig.Syntax.Inlines;

namespace BobrilMdx;

[DebuggerDisplay("`{Content}`")]
public class MdxCodeInline : LeafInline
{
    public MdxCodeInline(string content)
    {
        Content = content;
    }

    public int DelimiterCount { get; set; }

    /// <summary>
    /// Gets or sets the content of the span.
    /// </summary>
    public string Content { get; set; }
}