using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    public class AstImportExpression : AstNode
    {
        public AstNode ModuleName;

        public AstImportExpression(string? source, Position startLoc, Position endLoc, AstNode moduleName) : base(source, startLoc, endLoc)
        {
            ModuleName = moduleName;
        }

        public override void Visit(TreeWalker w)
        {
            w.Walk(ModuleName);
            base.Visit(w);
        }

        public override void Transform(TreeTransformer tt)
        {
            ModuleName = tt.Transform(ModuleName)!;
            base.Transform(tt);
        }

        public override AstNode ShallowClone()
        {
            return new AstImportExpression(Source, Start, End, ModuleName);
        }

        public override void CodeGen(OutputContext output)
        {
            output.Print("import");
            ModuleName.Print(output, true);
        }
    }
}
