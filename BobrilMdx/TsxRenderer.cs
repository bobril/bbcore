using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using Markdig.Extensions.Tables;
using Markdig.Helpers;
using Markdig.Renderers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Njsast.Output;

namespace BobrilMdx
{
    public class HtmlTableRenderer : TsxObjectRenderer<Table>
    {
        protected override void Write(TsxRenderer renderer, Table table)
        {
            renderer.EnsureLine();
            renderer.Write("<mdx.Table").WriteProps(table).Write('>').WriteLine();

            var hasBody = false;
            var hasAlreadyHeader = false;
            var isHeaderOpen = false;


            var hasColumnWidth = false;
            foreach (var tableColumnDefinition in table.ColumnDefinitions)
            {
                if (tableColumnDefinition.Width != 0.0f && tableColumnDefinition.Width != 1.0f)
                {
                    hasColumnWidth = true;
                    break;
                }
            }

            if (hasColumnWidth)
            {
                foreach (var tableColumnDefinition in table.ColumnDefinitions)
                {
                    var width = Math.Round(tableColumnDefinition.Width*100)/100;
                    var widthValue = string.Format(CultureInfo.InvariantCulture, "{0:0.##}", width);
                    renderer.Write($"<mdx.Col width=\"{widthValue}%\" />").WriteLine();
                }
            }

            foreach (var rowObj in table)
            {
                var row = (TableRow)rowObj;
                if (row.IsHeader)
                {
                    // Allow a single thead
                    if (!hasAlreadyHeader)
                    {
                        renderer.Write("<mdx.Thead>").WriteLine();
                        isHeaderOpen = true;
                    }
                    hasAlreadyHeader = true;
                }
                else if (!hasBody)
                {
                    if (isHeaderOpen)
                    {
                        renderer.Write("</mdx.Thead>").WriteLine();
                        isHeaderOpen = false;
                    }

                    renderer.Write("<mdx.Tbody>").WriteLine();
                    hasBody = true;
                }
                renderer.Write("<mdx.Tr").WriteProps(row).Write('>').WriteLine();
                for (var i = 0; i < row.Count; i++)
                {
                    var cellObj = row[i];
                    var cell = (TableCell)cellObj;

                    renderer.EnsureLine();
                    renderer.Write(row.IsHeader ? "<mdx.Th" : "<mdx.Td");
                    if (cell.ColumnSpan != 1)
                    {
                        renderer.Write($" colspan={{{cell.ColumnSpan}}}");
                    }
                    if (cell.RowSpan != 1)
                    {
                        renderer.Write($" rowspan={{{cell.RowSpan}}}");
                    }
                    if (table.ColumnDefinitions.Count > 0)
                    {
                        var columnIndex = cell.ColumnIndex < 0 || cell.ColumnIndex >= table.ColumnDefinitions.Count
                            ? i
                            : cell.ColumnIndex;
                        columnIndex = columnIndex >= table.ColumnDefinitions.Count ? table.ColumnDefinitions.Count - 1 : columnIndex;
                        var alignment = table.ColumnDefinitions[columnIndex].Alignment;
                        if (alignment.HasValue)
                        {
                            switch (alignment)
                            {
                                case TableColumnAlign.Center:
                                    renderer.Write(" align=\"center\"");
                                    break;
                                case TableColumnAlign.Right:
                                    renderer.Write(" align=\"right\"");
                                    break;
                                case TableColumnAlign.Left:
                                    renderer.Write(" align=\"left\"");
                                    break;
                            }
                        }
                    }
                    renderer.WriteProps(cell);
                    renderer.Write('>');

                    var previousImplicitParagraph = renderer.ImplicitParagraph;
                    if (cell.Count == 1)
                    {
                        renderer.ImplicitParagraph = true;
                    }
                    renderer.Write(cell);
                    renderer.ImplicitParagraph = previousImplicitParagraph;

                    renderer.Write(row.IsHeader ? "</mdx.Th>" : "</mdx.Td>").WriteLine();
                }
                renderer.Write("</mdx.Tr>").WriteLine();
            }

            if (hasBody)
            {
                renderer.Write("</mdx.Tbody>").WriteLine();
            }
            else if (isHeaderOpen)
            {
                renderer.Write("</mdx.Thead>").WriteLine();
            }
            renderer.Write("</mdx.Table>").WriteLine();
        }
    }

    public class TsxRenderer : RendererBase
    {
        readonly OutputContext _outputContext;

        public TsxRenderer()
        {
            _outputContext = new(new() {Beautify = true});
            // Default block renderers
            ObjectRenderers.Add(new AbbreviationRenderer());
            ObjectRenderers.Add(new CodeBlockRenderer());
            ObjectRenderers.Add(new ListRenderer());
            ObjectRenderers.Add(new HeadingRenderer());
            ObjectRenderers.Add(new HtmlBlockRenderer());
            ObjectRenderers.Add(new ParagraphRenderer());
            ObjectRenderers.Add(new QuoteBlockRenderer());
            ObjectRenderers.Add(new ThematicBreakRenderer());
            ObjectRenderers.Add(new HtmlTableRenderer());

            // Default inline renderers
            ObjectRenderers.Add(new AutolinkInlineRenderer());
            ObjectRenderers.Add(new CodeInlineRenderer());
            ObjectRenderers.Add(new DelimiterInlineRenderer());
            ObjectRenderers.Add(new EmphasisInlineRenderer());
            ObjectRenderers.Add(new LineBreakInlineRenderer());
            ObjectRenderers.Add(new HtmlInlineRenderer());
            ObjectRenderers.Add(new HtmlEntityInlineRenderer());
            ObjectRenderers.Add(new LinkInlineRenderer());
            ObjectRenderers.Add(new LiteralInlineRenderer());

            EnableHtmlForBlock = true;
            EnableHtmlForInline = true;
            EnableHtmlEscape = true;
        }

        /// <summary>
        /// Gets or sets a value indicating whether to output HTML tags when rendering. See remarks.
        /// </summary>
        /// <remarks>
        /// This is used by some renderers to disable HTML tags when rendering some inline elements (for image links).
        /// </remarks>
        public bool EnableHtmlForInline { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to output HTML tags when rendering. See remarks.
        /// </summary>
        /// <remarks>
        /// This is used by some renderers to disable HTML tags when rendering some block elements (for image links).
        /// </remarks>
        public bool EnableHtmlForBlock { get; set; }

        public bool EnableHtmlEscape { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to use implicit paragraph (optional &lt;p&gt;)
        /// </summary>
        public bool ImplicitParagraph { get; set; }

        static readonly IdnMapping IdnMapping = new();

        public TsxRenderer WriteEscapeUrl(string? content, bool isImage)
        {
            if (content is null)
                return this;

            // ab://c.d = 8 chars
            int schemeOffset = content.Length < 8 ? -1 : content.IndexOf("://", 2, StringComparison.Ordinal);
            if (schemeOffset != -1) // This is an absolute URL
            {
                Write("\"");
                schemeOffset += 3; // skip ://
                WriteEscapeUrl(content, 0, schemeOffset);

                bool idnaEncodeDomain = false;
                int endOfDomain = schemeOffset;
                for (; endOfDomain < content.Length; endOfDomain++)
                {
                    var c = content[endOfDomain];
                    if (c is '/' or '?' or '#' or ':') // End of domain part
                    {
                        break;
                    }
                    if (c > 127)
                    {
                        idnaEncodeDomain = true;
                    }
                }

                if (idnaEncodeDomain)
                {
                    string domainName;

                    try
                    {
                        domainName = IdnMapping.GetAscii(content, schemeOffset, endOfDomain - schemeOffset);
                    }
                    catch
                    {
                        // Not a valid IDN, fallback to non-punycode encoding
                        WriteEscapeUrl(content, schemeOffset, content.Length);
                        return this;
                    }

                    // Escape the characters (see Commonmark example 327 and think of it with a non-ascii symbol)
                    int previousPosition = 0;
                    for (int i = 0; i < domainName.Length; i++)
                    {
                        var escape = HtmlHelper.EscapeUrlCharacter(domainName[i]);
                        if (escape != null)
                        {
                            Write(domainName, previousPosition, i - previousPosition);
                            previousPosition = i + 1;
                            Write(escape);
                        }
                    }
                    Write(domainName, previousPosition, domainName.Length - previousPosition);
                    WriteEscapeUrl(content, endOfDomain, content.Length);
                }
                else
                {
                    WriteEscapeUrl(content, schemeOffset, content.Length);
                }
                Write("\"");
            }
            else // This is a relative URL
            {
                if (isImage)
                {
                    Write("{b.asset(");
                    WriteJsString(content);
                    Write(")}");
                }
                else
                {
                    Write("\"");
                    WriteEscapeUrl(content, 0, content.Length);
                    Write("\"");
                }
            }

            return this;
        }

        void WriteEscapeUrl(string content, int start, int length)
        {
            int previousPosition = start;
            for (var i = previousPosition; i < length; i++)
            {
                var c = content[i];

                if (c < 128)
                {
                    var escape = HtmlHelper.EscapeUrlCharacter(c);
                    if (escape != null)
                    {
                        Write(content, previousPosition, i - previousPosition);
                        previousPosition = i + 1;
                        Write(escape);
                    }
                }
                else
                {
                    Write(content, previousPosition, i - previousPosition);
                    previousPosition = i + 1;

                    Write(c);
                }
            }
            Write(content, previousPosition, length - previousPosition);
        }

        /// <summary>
        /// Writes the specified content.
        /// </summary>
        /// <param name="content">The content.</param>
        /// <returns>This instance</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TsxRenderer Write(string? content)
        {
            if (content is { })
            {
                if (_outputContext.WasNewLine()) _outputContext.Indent();
                _outputContext.Print(content);
            }
            return this;
        }

        /// <summary>
        /// Writes the specified slice.
        /// </summary>
        /// <param name="slice">The slice.</param>
        /// <returns>This instance</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TsxRenderer Write(ref StringSlice slice)
        {
            if (slice.Start > slice.End)
            {
                return (TsxRenderer) this;
            }
            return Write(slice.Text, slice.Start, slice.Length);
        }

        /// <summary>
        /// Writes the specified slice.
        /// </summary>
        /// <param name="slice">The slice.</param>
        /// <returns>This instance</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TsxRenderer Write(StringSlice slice)
        {
            return Write(ref slice);
        }

        /// <summary>
        /// Writes the specified character.
        /// </summary>
        /// <param name="content">The content.</param>
        /// <returns>This instance</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TsxRenderer Write(char content)
        {
            if (_outputContext.WasNewLine()) _outputContext.Indent();
            Span<char> buf = stackalloc char[1];
            buf[0] = content;
            _outputContext.Print(buf);
            return this;
        }

        /// <summary>
        /// Writes the inlines of a leaf inline.
        /// </summary>
        /// <param name="leafBlock">The leaf block.</param>
        /// <returns>This instance</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TsxRenderer WriteLeafInline(LeafBlock leafBlock)
        {
            var inline = (Inline) leafBlock.Inline!;

            while (inline != null)
            {
                Write(inline);
                inline = inline.NextSibling;
            }

            return this;
        }

        /// <summary>
        /// Writes the specified content.
        /// </summary>
        /// <param name="content">The content.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="length">The length.</param>
        /// <returns>This instance</returns>
        public TsxRenderer Write(string? content, int offset, int length)
        {
            if (content is null)
            {
                return this;
            }

            if (_outputContext.WasNewLine()) _outputContext.Indent();
            _outputContext.Print(content.AsSpan(offset, length));
            return this;
        }

        /// <summary>
        /// Writes the attached <see cref="TsxProps"/> on the specified <see cref="MarkdownObject"/>.
        /// </summary>
        /// <param name="markdownObject">The object.</param>
        /// <returns></returns>
        public TsxRenderer WriteProps(MarkdownObject markdownObject)
        {
            return WriteProps(markdownObject.TryGetProps());
        }

        public TsxRenderer WriteProps(TsxProps? attributes, Func<string, string>? classFilter = null)
        {
            if (attributes is null)
            {
                return this;
            }

            if (attributes.Id != null)
            {
                Write(" id=");
                _outputContext.PrintString(attributes.Id);
            }

            if (attributes.Classes is { Count: > 0 })
            {
                Write(" classes={[");
                for (int i = 0; i < attributes.Classes.Count; i++)
                {
                    var cssClass = attributes.Classes[i];
                    if (i > 0)
                    {
                        Write(',');
                    }
                    _outputContext.PrintString(classFilter != null ? classFilter(cssClass) : cssClass);
                }
                Write("]}");
            }

            if (attributes.Properties.Count > 0)
            {
                foreach (var property in attributes.Properties)
                {
                    Write(' ').Write(property.Key);
                    if (property.Code)
                    {
                        Write("={");
                        Write(property.Value as string);
                        Write('}');
                    }
                    else
                    {
                        Write("={");
                        Njsast.Runtime.TypeConverter.ToAst(property.Value).Print(_outputContext);
                        Write('}');
                    }
                }
            }

            return this;
        }

        /// <summary>
        /// Writes the lines of a <see cref="LeafBlock"/>
        /// </summary>
        /// <param name="leafBlock">The leaf block.</param>
        /// <param name="writeEndOfLines">if set to <c>true</c> write end of lines.</param>
        /// <param name="escape">if set to <c>true</c> escape the content for HTML</param>
        /// <param name="softEscape">Only escape &lt; and &amp;</param>
        /// <returns>This instance</returns>
        public TsxRenderer WriteLeafRawLines(LeafBlock leafBlock, bool writeEndOfLines, bool escape, bool softEscape = false)
        {
            if (leafBlock.Lines.Lines != null)
            {
                var lines = leafBlock.Lines;
                var slices = lines.Lines;

                if (lines.Count > 0)
                {
                    if (escape)
                    {
                        Write("{'");
                        for (var i = 0; i < lines.Count; i++)
                        {
                            if (!writeEndOfLines && i > 0)
                            {
                                _outputContext.Print("\\n");
                            }
                            _outputContext.PrintStringChars(slices[i].Slice.Text.AsSpan(slices[i].Slice.Start,slices[i].Slice.Length), QuoteType.Single);
                            if (writeEndOfLines)
                            {
                                _outputContext.Print("\\n");
                            }
                        }
                        Write("'}");
                    }
                    else
                    {
                        for (var i = 0; i < lines.Count; i++)
                        {
                            if (!writeEndOfLines && i > 0)
                            {
                                WriteLine();
                            }
                            Write(ref slices[i].Slice);
                            if (writeEndOfLines)
                            {
                                WriteLine();
                            }
                        }
                    }
                }
            }
            return this;
        }

        public TsxRenderer WriteLine()
        {
            _outputContext.Newline();
            return this;
        }

        public override object Render(MarkdownObject markdownObject)
        {
            Write(markdownObject);
            return _outputContext;
        }

        public TsxRenderer EnsureLine()
        {
            if (!_outputContext.WasNewLine()) _outputContext.Newline();
            return this;
        }

        public TsxRenderer Dedent()
        {
            _outputContext.Indentation-=4;
            return this;
        }

        public TsxRenderer Indent()
        {
            _outputContext.Indentation+=4;
            return this;
        }

        public void WriteEscape(ref StringSlice content)
        {
            Write("{");
            _outputContext.PrintString(content.Text.AsSpan(content.Start,content.Length));
            Write("}");
        }

        public void WriteEscape(string content)
        {
            Write("{");
            _outputContext.PrintString(content);
            Write("}");
        }

        public TsxRenderer WriteJsString(string str)
        {
            _outputContext.PrintString(str);
            return this;
        }
    }
}
