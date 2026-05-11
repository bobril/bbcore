using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast;

public class AstImportExpression : AstNode
{
    public AstNode ModuleName;
    public AstNode? Options;

    public AstImportExpression(string? source, Position startLoc, Position endLoc, AstNode moduleName,
        AstNode? options = null) : base(source, startLoc, endLoc)
    {
        ModuleName = moduleName;
        Options = options;
    }

    public override void Visit(TreeWalker w)
    {
        w.Walk(ModuleName);
        w.Walk(Options);
        base.Visit(w);
    }

    public override void Transform(TreeTransformer tt)
    {
        ModuleName = tt.Transform(ModuleName)!;
        if (Options != null)
            Options = tt.Transform(Options);
        base.Transform(tt);
    }

    public override AstNode ShallowClone()
    {
        return new AstImportExpression(Source, Start, End, ModuleName, Options);
    }

    public override void CodeGen(OutputContext output)
    {
        output.Print("import");
        if (Options == null)
        {
            ModuleName.Print(output, true);
            return;
        }

        output.Print("(");
        ModuleName.Print(output);
        output.Comma();
        Options.Print(output);
        output.Print(")");
    }
}
