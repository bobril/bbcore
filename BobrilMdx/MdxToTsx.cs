using System.Collections.Generic;
using System.IO;
using System.Linq;
using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Parsers.Inlines;
using Markdig.Syntax;
using Njsast.Output;
using Njsast.Runtime;
using YamlDotNet.Serialization;

namespace BobrilMdx
{
    public class MdxToTsx
    {
        readonly MarkdownPipeline _pipeline;
        MarkdownDocument? _document;

        public MdxToTsx()
        {
            var builder = new MarkdownPipelineBuilder()
                .UseYamlFrontMatter()
                .UseAbbreviations()
                .UseAutoIdentifiers()
                .UseCitations()
                .UseDefinitionLists()
                .UseEmphasisExtras()
                .UseFigures()
                .UseFooters()
                //.UseFootnotes() too complex for small usefulness
                .UseGridTables()
                //.UseMathematics()
                //.UseMediaLinks() could be probably solved in component
                .UsePipeTables()
                .UseListExtras()
                .UseTaskLists()
                .UseDiagrams()
                .DisableHtml()
                .UseAutoLinks();
            builder.Extensions.AddIfNotAlready<ImportExtension>();
            builder.BlockParsers.AddIfNotAlready<MdxBlockParser>();
            builder.InlineParsers.AddIfNotAlready<MdxCodeInlineParser>();
            builder.InlineParsers.TryRemove<AutolinkInlineParser>();
            builder.InlineParsers.AddIfNotAlready<AutolinkAndJsxInlineParser>();
            _pipeline = builder.Build();
        }

        public void Parse(string text)
        {
            _document = Markdown.Parse(text, _pipeline);
        }

        public (string content, Dictionary<object, object> metadata) Render()
        {
            var renderer = new TsxRenderer();
            renderer.Write("import * as b from \"bobril\";").WriteLine();
            renderer.Write("import * as mdx from \"@bobril/mdx\";").WriteLine();

            foreach (var importBlock in _document!.Descendants<ImportBlock>())
            {
                renderer.Write(importBlock.Lines.ToSlice()).WriteLine();
            }

            var frontMatterBlock = _document!
                .Descendants<YamlFrontMatterBlock>()
                .FirstOrDefault();
            Dictionary<object, object>? metadata = null;
            if (frontMatterBlock != null)
            {
                var yaml = frontMatterBlock.Lines.ToString();
                var deserializer = new DeserializerBuilder().Build();
                var yamlObject = deserializer.Deserialize(new StringReader(yaml));
                metadata = yamlObject as Dictionary<object, object>;
                frontMatterBlock.Parent!.Remove(frontMatterBlock);
            }

            metadata ??= new();
            renderer.Write("export const metadata = ").Write(TypeConverter.ToAst(metadata)).Write(";").WriteLine();
            metadata.TryGetValue("DataType", out var dataType);
            var dataTypeStr = dataType as string;
            renderer.Write("export default b.component(("+(dataTypeStr!=null?"data: "+dataTypeStr:"")+") => { return (<>").WriteLine()
                .Indent();
            var output = (OutputContext) renderer.Render(_document!);
            renderer.Dedent().EnsureLine().Write("</>);});").WriteLine();
            return (output.ToString(), metadata);
        }
    }
}
