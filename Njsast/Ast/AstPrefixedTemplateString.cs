using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// A templatestring with a prefix, such as String.raw`foobarbaz`
    public class AstPrefixedTemplateString : AstNode
    {
        /// [AstTemplateString] The template string
        public AstTemplateString TemplateString;

        /// [AstSymbolRef|AstPropAccess] The prefix, which can be a symbol such as `foo` or a dotted expression such as `String.raw`.
        public AstNode Prefix;

        public AstPrefixedTemplateString(string? source, Position startLoc, Position endLoc, AstNode prefix,
            AstTemplateString templateString) : base(source, startLoc, endLoc)
        {
            TemplateString = templateString;
            Prefix = prefix;
        }

        public override void Visit(TreeWalker w)
        {
            base.Visit(w);
            w.Walk(Prefix);
            w.Walk(TemplateString);
        }

        public override void Transform(TreeTransformer tt)
        {
            base.Transform(tt);
            Prefix = tt.Transform(Prefix);
            TemplateString = (AstTemplateString)tt.Transform(TemplateString);
        }

        public override AstNode ShallowClone()
        {
            return new AstPrefixedTemplateString(Source, Start, End, Prefix, TemplateString);
        }

        public override void CodeGen(OutputContext output)
        {
            var parenthesizeTag = Prefix is AstArrow
                                  || Prefix is AstBinary
                                  || Prefix is AstConditional
                                  || Prefix is AstSequence
                                  || Prefix is AstUnary;
            if (parenthesizeTag) output.Print("(");
            Prefix.Print(output);
            if (parenthesizeTag) output.Print(")");
            TemplateString.Print(output);
        }
    }
}
