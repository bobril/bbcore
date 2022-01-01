using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast;

/// An ES6 Arrow function ((a) => b)
public class AstArrow : AstLambda
{
    public AstArrow(string? source, Position startPos, Position endPos, AstSymbolDeclaration? name,
        ref StructList<AstNode> argNames, bool isGenerator, bool async, ref StructList<AstNode> body) : base(source,
        startPos, endPos, name, ref argNames, isGenerator, async, ref body)
    {
    }

    public AstArrow()
    {
    }

    AstArrow(string? source, Position startPos, Position endPos, AstSymbolDeclaration? name, bool isGenerator, bool async) : base(source, startPos, endPos, name, isGenerator, async)
    {
    }

    public override AstNode ShallowClone()
    {
        var res = new AstArrow(Source, Start, End, Name, IsGenerator, Async);
        res.Body.AddRange(Body.AsReadOnlySpan());
        res.ArgNames.AddRange(ArgNames.AsReadOnlySpan());
        res.HasUseStrictDirective = HasUseStrictDirective;
        res.Pure = Pure;
        return res;
    }

    public override void DoPrint(OutputContext output, bool noKeyword = false)
    {
        var parent = output.Parent();
        var needsParens = parent is AstBinary or AstUnary || parent is AstCall call && this == call.Expression;
        if (needsParens)
            output.Print("(");
        if (Async)
        {
            output.Print("async");
            output.Space();
        }

        if (ArgNames.Count == 1 && ArgNames[0] is AstSymbol)
        {
            ArgNames[0].Print(output);
        }
        else
        {
            output.Print("(");
            for (var i = 0u; i < ArgNames.Count; i++)
            {
                if (i > 0)
                    output.Comma();
                ArgNames[i].Print(output);
            }

            output.Print(")");
        }

        output.Space();
        output.Print("=>");
        output.Space();
        PrintArrowBody(output);
        if (needsParens)
            output.Print(")");
    }

    void PrintArrowBody(OutputContext output)
    {
        if (Body.Count == 1)
        {
            var last = Body.Last;
            if (last.IsExpression())
            {
                last.Print(output);
                return;
            }
        }

        output.PrintBraced(Body, false);
    }

    public override bool NeedParens(OutputContext output)
    {
        var p = output.Parent();
        return p is AstPropAccess propAccess && propAccess.Expression == this;
    }
}