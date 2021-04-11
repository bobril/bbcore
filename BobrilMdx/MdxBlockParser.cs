using Markdig.Parsers;
using Markdig.Syntax;

namespace BobrilMdx
{
    public class MdxBlockParser : BlockParser
    {
        public MdxBlockParser()
        {
            OpeningCharacters = new[] { '<' };
        }

        public override BlockState TryOpen(BlockProcessor processor)
        {
            if (processor.IsCodeIndent)
            {
                return BlockState.None;
            }

            if (processor.Line[processor.Line.End] is not '>')
            {
                return BlockState.None;
            }

            return CreateHtmlBlock(processor, HtmlBlockType.InterruptingBlock, processor.Column, processor.CurrentLineStartPosition);
        }

        public override BlockState TryContinue(BlockProcessor processor, Block block)
        {
            if (processor.IsBlankLine)
            {
                return BlockState.BreakDiscard;
            }

            return BlockState.Continue;
        }

        BlockState CreateHtmlBlock(BlockProcessor state, HtmlBlockType type, int startColumn, int startPosition)
        {
            state.NewBlocks.Push(new HtmlBlock(this)
            {
                Column = startColumn,
                Type = type,
                Span = new(startPosition, startPosition + state.Line.End),
                NewLine = state.Line.NewLine,
            });
            return BlockState.Continue;
        }
    }
}
