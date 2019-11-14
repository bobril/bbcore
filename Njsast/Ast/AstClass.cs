using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// An ES6 class
    public class AstClass : AstScope
    {
        /// [AstSymbolClass|AstSymbolDefClass?] optional class name.
        public AstSymbolDeclaration? Name;

        /// [AstNode]? optional parent class
        public AstNode? Extends;

        /// [AstObjectProperty*] array of properties
        public StructList<AstObjectProperty> Properties;

        public bool Inlined;

        public AstClass(string? source, Position startPos, Position endPos, AstSymbolDeclaration? name, AstNode? extends,
            ref StructList<AstObjectProperty> properties) : base(source, startPos, endPos)
        {
            Name = name;
            Extends = extends;
            Properties.TransferFrom(ref properties);
        }

        public override void Visit(TreeWalker w)
        {
            base.Visit(w);
            w.Walk(Name);
            w.Walk(Extends);
            w.WalkList(Properties);
        }

        public override void Transform(TreeTransformer tt)
        {
            base.Transform(tt);
            if (Name != null)
                Name = (AstSymbolDeclaration)tt.Transform(Name);
            if (Extends != null)
                Extends = tt.Transform(Extends);
            tt.TransformList(ref Properties);
        }

        public override void CodeGen(OutputContext output)
        {
            output.Print("class");
            output.Space();
            if (Name != null)
            {
                Name.Print(output);
                output.Space();
            }

            if (Extends != null)
            {
                var parens = !(Extends is AstSymbolRef)
                             && !(Extends is AstPropAccess)
                             && !(Extends is AstClassExpression)
                             && !(Extends is AstFunction);
                output.Print("extends");
                if (!parens)
                {
                    output.Space();
                }

                Extends.Print(output, parens);
                if (!parens)
                {
                    output.Space();
                }
            }

            if (Properties.Count > 0)
            {
                output.Print("{");
                output.Newline();
                output.Indentation += output.Options.IndentLevel;
                output.Indent();
                for (var i = 0u; i < Properties.Count; i++)
                {
                    if (i > 0)
                    {
                        output.Newline();
                    }

                    output.Indent();
                    Properties[i].Print(output);
                }

                output.Newline();
                output.Indentation -= output.Options.IndentLevel;
                output.Indent();
                output.Print("}");
            }
            else
            {
                output.Print("{}");
            }
        }

        public override bool IsBlockScope => false;
    }
}
