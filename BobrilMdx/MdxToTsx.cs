using System.IO;
using System.Text;
using Markdig;
using Markdig.Syntax;
using Njsast.Output;

namespace BobrilMdx
{
    public class MdxToTsx
    {
        readonly MarkdownPipeline _pipeline;
        MarkdownDocument? _document;

        public MdxToTsx()
        {
            _pipeline = new MarkdownPipelineBuilder()
                    .UseAbbreviations()
                    .UseAutoIdentifiers()
                    //.UseCitations()
                    //.UseCustomContainers() <div> <span> low priority
                    //.UseDefinitionLists()
                    .UseEmphasisExtras()
                    //.UseFigures()
                    //.UseFooters()
                    //.UseFootnotes()
                    .UseGridTables()
                    //.UseMathematics()
                    //.UseMediaLinks() could be probably solved in component
                    .UsePipeTables()
                    .UseListExtras()
                    //.UseTaskLists()
                    .UseDiagrams()
                    .UseAutoLinks()
                    .UseGenericAttributes() // Must be last as it is one parser that is modifying other parsers
                    .Build();
        }

        public void Parse(string text)
        {
            _document = Markdown.Parse(text, _pipeline);
        }

        public string Render()
        {
            var renderer = new TsxRenderer();
            renderer.Write("import * as b from \"bobril\";").WriteLine();
            renderer.Write("import * as mdx from \"@bobril/mdx\";").WriteLine();
            renderer.Write("export const metadata = {};").WriteLine();
            renderer.Write("export default b.component((_data: Record<string, any>) => { return (<>").WriteLine().Indent();
            var output = (OutputContext) renderer.Render(_document!);
            renderer.Dedent().EnsureLine().Write("</>);});").WriteLine();
            return output.ToString();
        }
    }
}
