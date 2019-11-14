using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// An `import` statement
    public class AstImport : AstStatement
    {
        /// [AstSymbolImport] The name of the variable holding the module's default export.
        public AstSymbolImport? ImportedName; // TODO not sure if it is correct that ImportName should be null

        /// [AstNameMapping*] The names of non-default imported variables
        public StructList<AstNameMapping> ImportedNames;

        /// [AstString] String literal describing where this module came from
        public AstString ModuleName;

        public AstImport(string? source, Position startLoc, Position endLoc, AstString moduleName,
            AstSymbolImport? importName, ref StructList<AstNameMapping> specifiers) : base(source, startLoc, endLoc)
        {
            ModuleName = moduleName;
            ImportedName = importName;
            ImportedNames.TransferFrom(ref specifiers);
        }

        AstImport(string? source, Position startLoc, Position endLoc, AstString moduleName,
            AstSymbolImport? importName) : base(source, startLoc, endLoc)
        {
            ModuleName = moduleName;
            ImportedName = importName;
        }

        public override void Visit(TreeWalker w)
        {
            base.Visit(w);
            w.Walk(ModuleName);
            w.Walk(ImportedName);
            w.WalkList(ImportedNames);
        }

        public override void Transform(TreeTransformer tt)
        {
            base.Transform(tt);
            ModuleName = (AstString) tt.Transform(ModuleName);
            if (ImportedName != null)
                ImportedName = (AstSymbolImport)tt.Transform(ImportedName);
            tt.TransformList(ref ImportedNames);
        }

        public override AstNode ShallowClone()
        {
            var res = new AstImport(Source, Start, End, ModuleName, ImportedName);
            res.ImportedNames.AddRange(ImportedNames);
            return res;
        }

        public override void CodeGen(OutputContext output)
        {
            output.Print("import");
            output.Space();
            ImportedName?.Print(output);
            if (ImportedName != null && ImportedNames.Count > 0)
            {
                output.Print(",");
                output.Space();
            }

            if (ImportedNames.Count > 0)
            {
                if (ImportedNames.Count == 1 && ImportedNames[0].ForeignName.Name == "*")
                {
                    ImportedNames[0].Print(output);
                }
                else
                {
                    output.Print("{");
                    for (var i = 0u; i < ImportedNames.Count; i++)
                    {
                        if (i > 0) output.Comma();
                        else
                            output.Space();
                        ImportedNames[i].Print(output);
                    }

                    output.Space();
                    output.Print("}");
                }
            }

            if (ImportedName != null || ImportedNames.Count > 0)
            {
                output.Space();
                output.Print("from");
                output.Space();
            }

            ModuleName.Print(output);
            output.Semicolon();
        }
    }
}
