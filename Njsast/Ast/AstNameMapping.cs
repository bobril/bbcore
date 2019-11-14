using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// The part of the export/import statement that declare names from a module.
    public class AstNameMapping : AstNode
    {
        /// [AstSymbolExportForeign|AstSymbolImportForeign] The name being exported/imported (as specified in the module)
        public AstSymbol ForeignName;

        /// [AstSymbolExport|AstSymbolImport] The name as it is visible to this module.
        public AstSymbol Name;

        public AstNameMapping(string? source, Position startLoc, Position endLoc, AstSymbol foreignName, AstSymbol name)
            : base(source, startLoc, endLoc)
        {
            ForeignName = foreignName;
            Name = name;
        }

        public override void Visit(TreeWalker w)
        {
            base.Visit(w);
            w.Walk(Name);
            w.Walk(ForeignName);
        }

        public override void Transform(TreeTransformer tt)
        {
            base.Transform(tt);
            Name = (AstSymbol)tt.Transform(Name);
            ForeignName = (AstSymbol)tt.Transform(ForeignName);
        }

        public override AstNode ShallowClone()
        {
            return new AstNameMapping(Source, Start, End, ForeignName, Name);
        }

        public override void CodeGen(OutputContext output)
        {
            var isImport = output.Parent() is AstImport;
            var definition = Name.Thedef;
            var namesAreDifferent =
                (definition?.MangledName ?? Name.Name) !=
                ForeignName.Name;
            if (namesAreDifferent)
            {
                if (isImport)
                {
                    output.Print(ForeignName.Name);
                }
                else
                {
                    Name.Print(output);
                }

                output.Space();
                output.Print("as");
                output.Space();
                if (isImport)
                {
                    Name.Print(output);
                }
                else
                {
                    output.Print(ForeignName.Name);
                }
            }
            else
            {
                Name.Print(output);
            }
        }
    }
}
