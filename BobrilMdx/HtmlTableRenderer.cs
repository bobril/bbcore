using System;
using System.Globalization;
using Markdig.Extensions.Tables;

namespace BobrilMdx;

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