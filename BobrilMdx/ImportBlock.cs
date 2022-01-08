using Markdig;
using Markdig.Parsers;
using Markdig.Renderers;
using Markdig.Syntax;

namespace BobrilMdx;

public class ImportBlock : LeafBlock
{
    public ImportBlock(BlockParser parser) : base(parser)
    {
    }
}

public class ImportRenderer : TsxObjectRenderer<ImportBlock>
{
    protected override void Write(TsxRenderer renderer, ImportBlock obj)
    {
    }
}

public class ImportParser : BlockParser
{
    public ImportParser()
    {
        OpeningCharacters = new[] { 'i' };
    }

    public override bool CanInterrupt(BlockProcessor processor, Block block)
    {
        return true;
    }

    /// <summary>
    /// Tries to match a block opening.
    /// </summary>
    /// <param name="processor">The parser processor.</param>
    /// <returns>The result of the match</returns>
    public override BlockState TryOpen(BlockProcessor processor)
    {
        // import must start at start of line
        if (processor.Column > 0)
        {
            return BlockState.None;
        }

        var line = processor.Line;

        if (!line.Match("import ",line.Start+7, 0) || line[line.End] is not ';')
            return BlockState.None;

        var block = new ImportBlock(this)
        {
            Column = processor.Column,
            Span = new(line.Start, line.End),
            NewLine = processor.Line.NewLine,
        };
        processor.NewBlocks.Push(block);

        return BlockState.Break;
    }
}

public class ImportExtension : IMarkdownExtension
{
    public void Setup(MarkdownPipelineBuilder pipeline)
    {
        pipeline.BlockParsers.AddIfNotAlready(new ImportParser());
    }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
    }
}