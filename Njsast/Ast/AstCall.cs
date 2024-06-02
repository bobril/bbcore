using Njsast.AstDump;
using Njsast.ConstEval;
using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast;

/// A function call expression
public class AstCall : AstNode
{
    /// [AstNode] expression to invoke as function
    public AstNode Expression;

    /// [AstNode*] array of arguments
    public StructList<AstNode> Args;

    public bool Optional;

    public AstCall(string? source, Position startLoc, Position endLoc, AstNode expression,
        ref StructList<AstNode> args, bool optional = false) : base(source, startLoc, endLoc)
    {
        Expression = expression;
        Optional = optional;
        Args.TransferFrom(ref args);
    }

    protected AstCall(string? source, Position startLoc, Position endLoc, AstNode expression, bool optional = false) : base(source, startLoc, endLoc)
    {
        Expression = expression;
        Optional = optional;
    }

    public AstCall(AstNode expression)
    {
        Expression = expression;
    }

    public override void Visit(TreeWalker w)
    {
        base.Visit(w);
        w.Walk(Expression);
        w.WalkList(Args);
    }

    public override void Transform(TreeTransformer tt)
    {
        base.Transform(tt);
        Expression = tt.Transform(Expression)!;
        tt.TransformList(ref Args);
    }

    public override AstNode ShallowClone()
    {
        var res = new AstCall(Source, Start, End, Expression, Optional);
        res.Args.AddRange(Args.AsReadOnlySpan());
        return res;
    }

    public override void CodeGen(OutputContext output)
    {
        Expression.Print(output);
        if (this is AstNew && !output.NeedConstructorParens(this))
            return;
        if (Expression is AstCall or AstLambda)
        {
            output.AddMapping(Expression.Source, Start, false);
        }

        if (Optional)
            output.Print("?.");
        output.Print("(");
        for (var i = 0u; i < Args.Count; i++)
        {
            if (i > 0) output.Comma();
            Args[i].Print(output);
        }

        output.Print(")");
    }

    public override bool NeedParens(OutputContext output)
    {
        var p = output.Parent();
        if (p is AstNew aNew && aNew.Expression == this
            || p is AstExport { IsDefault: true } && Expression is AstFunction)
            return true;

        return false;
    }

    public override object? ConstValue(IConstEvalCtx? ctx = null)
    {
        if (Expression is AstSymbolRef symb)
        {
            var def = symb.Thedef;
            if (def == null || ctx == null || Args.Count != 1) return null;
            if (def.Undeclared && def.Global && def.Name == "require")
            {
                var param = Args[0].ConstValue(ctx.StripPathResolver());
                if (param is not string s) return null;
                return ctx.ResolveRequire(s);
            }
        }

        return null;
    }

    public override void DumpScalars(IAstDumpWriter writer)
    {
        base.DumpScalars(writer);
        writer.PrintProp("Optional", Optional);
    }

    public override bool IsStructurallyEquivalentTo(AstNode? with)
    {
        if (with is AstCall astCall)
        {
            if (!Expression.IsStructurallyEquivalentTo(astCall.Expression)) return false;
            if (Args.Count != astCall.Args.Count) return false;
            for (var i = 0; i < Args.Count; i++)
            {
                if (!Args[i].IsStructurallyEquivalentTo(astCall.Args[i])) return false;
            }

            return true;
        }

        return false;
    }
}
