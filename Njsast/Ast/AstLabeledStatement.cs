using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// Statement with a label
    public class AstLabeledStatement : AstStatementWithBody
    {
        /// [AstLabel] a label definition
        public AstLabel Label;

        public AstLabeledStatement(string? source, Position startPos, Position endPos, AstStatement body, AstLabel label) : base(source, startPos, endPos, body)
        {
            Label = label;
        }

        public override void Visit(TreeWalker w)
        {
            w.Walk(Label);
            base.Visit(w);
        }

        public override void Transform(TreeTransformer tt)
        {
            Label = (AstLabel)tt.Transform(Label);
            base.Transform(tt);
        }

        public override AstNode ShallowClone()
        {
            return new AstLabeledStatement(Source, Start, End, Body, Label);
        }

        public override void CodeGen(OutputContext output)
        {
            var name = Label.MangledName ?? Label.Name;
            output.PrintName(name);
            output.Colon();
            Body.Print(output);
        }
    }
}
