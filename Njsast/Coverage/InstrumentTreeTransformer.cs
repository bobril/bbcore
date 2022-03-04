using Njsast.Ast;
using Njsast.Reader;

namespace Njsast.Coverage;

class InstrumentTreeTransformer : TreeTransformer
{
    readonly CoverageInstrumentation _owner;

    public InstrumentTreeTransformer(CoverageInstrumentation owner)
    {
        _owner = owner;
    }

    protected override AstNode? Before(AstNode node, bool inList)
    {
        switch (node)
        {
            case AstIf astIf:
            {
                astIf.Body = (AstStatement) Transform(MakeBlockStatement(astIf.Body));
                if (astIf.Alternative != null)
                {
                    astIf.Alternative = (AstStatement) Transform(MakeBlockStatement(astIf.Alternative!));
                }

                astIf.Condition = InstrumentCondition(astIf.Condition);
                astIf.Condition = Transform(astIf.Condition);

                return node;
            }
            case AstBinary astBinary:
            {
                if (astBinary.Operator is Operator.LogicalAnd or Operator.LogicalOr or Operator.NullishCoalescing)
                {
                    astBinary.Right = Transform(astBinary.Right);
                    astBinary.Left = InstrumentCondition(astBinary.Left);
                    astBinary.Left = Transform(astBinary.Left);
                    return node;
                }

                return null;
            }
            case AstBlockStatement blockStatement:
                InstrumentBlock(ref blockStatement.Body);
                return node;
            case AstTry astTry:
            {
                InstrumentBlock(ref astTry.Body);
                if (astTry.Bcatch != null) astTry.Bcatch = (AstCatch) Transform(astTry.Bcatch);
                if (astTry.Bfinally != null) astTry.Bfinally = (AstFinally) Transform(astTry.Bfinally);
                return node;
            }
            case AstCatch astCatch:
            {
                if (astCatch.Argname != null) astCatch.Argname = Transform(astCatch.Argname);
                InstrumentBlock(ref astCatch.Body);
                return node;
            }
            case AstFinally astFinally:
                InstrumentBlock(ref astFinally.Body);
                return node;
            case AstConditional astConditional:
                astConditional.Condition = InstrumentCondition(astConditional.Condition);
                astConditional.Consequent = Transform(astConditional.Consequent);
                astConditional.Alternative = Transform(astConditional.Alternative);
                return node;
            case AstSequence astSequence:
                InstrumentBlock(ref astSequence.Expressions, true);
                return node;
            case AstLambda lambda:
                InstrumentFunction(lambda);
                return node;
            case AstSwitchBranch astSwitchBranch:
                InstrumentBlock(ref astSwitchBranch.Body);
                return InstrumentSwitchCase(astSwitchBranch);
            case AstDwLoop astWhile:
                astWhile.Condition = InstrumentCondition(astWhile.Condition);
                astWhile.Body = (AstStatement) Transform(MakeBlockStatement(astWhile.Body));
                return node;
            case AstFor astFor:
            {
                if (astFor.Init != null) astFor.Init = Transform(astFor.Init);
                if (astFor.Condition != null) astFor.Condition = InstrumentCondition(astFor.Condition);
                if (astFor.Step != null) astFor.Step = InstrumentExpression(Transform(astFor.Step));
                astFor.Body = (AstStatement) Transform(MakeBlockStatement(astFor.Body));
                return node;
            }
            case AstForIn astForIn:
                astForIn.Init = Transform(astForIn.Init);
                astForIn.Object = Transform(astForIn.Object);
                astForIn.Body = (AstStatement) Transform(MakeBlockStatement(astForIn.Body));
                return node;
            case AstWith astWith:
                astWith.Expression = Transform(astWith.Expression);
                astWith.Body = (AstStatement) Transform(MakeBlockStatement(astWith.Body));
                return node;
            case AstToplevel astToplevel:
                InstrumentBlock(ref astToplevel.Body);
                return node;
            default:
                return null;
        }
    }

    void InstrumentFunction(AstLambda lambda)
    {
        if (lambda is AstArrow arrow)
        {
            if (arrow.Body.Count == 1 && arrow.Body.Last.IsExpression())
            {
                var original = arrow.Body[0];
                arrow.Body[0] = new AstBlock(original)
                    { Body = new() { new AstReturn(original) { Value = original } } };
            }
        }
        if (lambda.Source == null)
        {
            InstrumentBlock(ref lambda.Body);
            return;
        }

        var idx = _owner.LastIndex++;
        var call = new AstCall(new AstSymbolRef(_owner.FncNameStatement));
        call.Args.Add(new AstNumber(idx));
        lambda.Body.Insert(0) = new AstSimpleStatement(call);
        _owner.GetForFile(lambda.Source)
            .AddInfo(new InstrumentedInfo(InstrumentedInfoType.Function, idx, lambda.Start, lambda.End));
        var input = new StructList<AstNode>();
        input.TransferFrom(ref lambda.Body);
        lambda.Body.Reserve(input.Count * 2 - 1);
        lambda.Body.Add(input[0]);
        for (var i = 1; i < input.Count; i++)
        {
            var ii = input[i];
            if (ShouldStatementCover(ii))
            {
                if (i != 1)
                {
                    idx = _owner.LastIndex++;
                    call = new AstCall(new AstSymbolRef(_owner.FncNameStatement));
                    call.Args.Add(new AstNumber(idx));
                    lambda.Body.Add(new AstSimpleStatement(call));
                }

                _owner.GetForFile(ii.Source!)
                    .AddInfo(new InstrumentedInfo(InstrumentedInfoType.Statement, idx, ii.Start, ii.End));
            }

            ii = Transform(ii);
            lambda.Body.Add(ii);
        }
    }

    AstNode InstrumentExpression(AstNode node)
    {
        if (node is AstSequence) return node;
        if (node.Source == null) return node;
        var res = new AstSequence(node);
        var idx = _owner.LastIndex++;
        var call = new AstCall(new AstSymbolRef(_owner.FncNameStatement));
        call.Args.Add(new AstNumber(idx));
        res.Expressions.Add(call);
        res.Expressions.Add(node);
        _owner.GetForFile(node.Source)
            .AddInfo(new InstrumentedInfo(InstrumentedInfoType.Statement, idx, node.Start, node.End));
        return res;
    }

    void InstrumentBlock(ref StructList<AstNode> block, bool seq = false)
    {
        var input = new StructList<AstNode>();
        input.TransferFrom(ref block);
        block.Reserve(input.Count * 2);
        for (var i = 0; i < input.Count; i++)
        {
            var ii = input[i];
            if (ShouldStatementCover(ii))
            {
                var idx = _owner.LastIndex++;
                var call = new AstCall(new AstSymbolRef(_owner.FncNameStatement));
                call.Args.Add(new AstNumber(idx));
                if (seq)
                {
                    block.Add(call);
                }
                else
                {
                    block.Add(new AstSimpleStatement(call));
                }

                _owner.GetForFile(ii.Source!)
                    .AddInfo(new InstrumentedInfo(InstrumentedInfoType.Statement, idx, ii.Start, ii.End));
            }

            ii = Transform(ii);
            block.Add(ii);
        }
    }

    static bool ShouldStatementCover(AstNode node)
    {
        if (node.Source == null)
            return false;
        if (node is AstDefun || node is AstClass || node is AstImport || node is AstExport)
            return false;
        if (node.IsExportsAssign(true).HasValue)
            return false;
        if (node.IsDefinePropertyExportsEsModule())
            return false;
        if (IsVarRequire(node))
            return false;
        return true;
    }

    static bool IsVarRequire(AstNode node)
    {
        if (node is AstVar astVar)
        {
            if (astVar.Definitions.Count == 1 && astVar.Definitions[0].Value is AstCall call)
            {
                return IsRequireCall(call);
            }
        }

        if (node is AstSimpleStatement astSimpleStatement)
        {
            if (astSimpleStatement.Body is AstCall call)
            {
                return IsRequireCall(call);
            }
        }

        return false;
    }

    static bool IsRequireCall(AstCall call)
    {
        if (call.Args.Count == 1 && call.Expression is AstSymbolRef symb && symb.Name == "require" &&
            call.Args[0] is AstString)
            return true;
        return false;
    }

    static bool ShouldConditionCover(AstNode node)
    {
        if (node.Source == null)
            return false;
        // Don't cover trivial conditions
        if (node is AstFalse || node is AstTrue)
            return false;
        return true;
    }

    static AstBlockStatement MakeBlockStatement(AstStatement statement)
    {
        if (statement is AstBlockStatement blockStatement) return blockStatement;
        var res = new AstBlockStatement(statement);
        res.Body.Add(statement);
        return res;
    }

    AstNode InstrumentCondition(AstNode condition)
    {
        if (!ShouldConditionCover(condition))
            return condition;
        var idx = _owner.LastIndex;
        _owner.LastIndex += 2;
        var res = new AstCall(new AstSymbolRef(_owner.FncNameCond));
        res.Args.Add(condition);
        res.Args.Add(new AstNumber(idx));
        _owner.GetForFile(condition.Source!)
            .AddInfo(new InstrumentedInfo(InstrumentedInfoType.Condition, idx, condition.Start, condition.End));
        return res;
    }

    AstSwitchBranch InstrumentSwitchCase(AstSwitchBranch branch)
    {
        if (branch.Source == null) return branch;
        var idx = _owner.LastIndex++;
        var call = new AstCall(new AstSymbolRef(_owner.FncNameStatement));
        call.Args.Add(new AstNumber(idx));
        branch.Body.Insert(0) = new AstSimpleStatement(call);
        _owner.GetForFile(branch.Source)
            .AddInfo(new InstrumentedInfo(InstrumentedInfoType.SwitchBranch, idx, branch.Start, branch.End));
        return branch;
    }

    protected override AstNode? After(AstNode node, bool inList)
    {
        return null;
    }
}
