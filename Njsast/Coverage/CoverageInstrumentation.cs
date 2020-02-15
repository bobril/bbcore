using Njsast.Ast;
using Njsast.Reader;

namespace Njsast.Coverage
{
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
                    astIf.Condition = Transform(astIf.Condition);
                    astIf.Condition = InstrumentCondition(astIf.Condition);
                    astIf.Body = (AstStatement) Transform(MakeBlockStatement(astIf.Body));
                    if (astIf.Alternative != null)
                    {
                        astIf.Alternative = (AstStatement) Transform(MakeBlockStatement(astIf.Alternative!));
                    }

                    return node;
                }
                case AstBinary astBinary:
                {
                    if (astBinary.Operator == Operator.LogicalAnd || astBinary.Operator == Operator.LogicalOr)
                    {
                        astBinary.Left = Transform(astBinary.Left);
                        astBinary.Left = InstrumentCondition(astBinary.Left);
                        astBinary.Right = Transform(astBinary.Right);
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
                    InstrumentBlock(ref lambda.Body);
                    return node;
                case AstSwitchBranch astSwitchBranch:
                    InstrumentBlock(ref astSwitchBranch.Body);
                    return node;
                case AstDwLoop astWhile:
                    astWhile.Condition = InstrumentCondition(astWhile.Condition);
                    astWhile.Body = (AstStatement) Transform(MakeBlockStatement(astWhile.Body));
                    return node;
                case AstFor astFor:
                {
                    if (astFor.Init != null) astFor.Init = Transform(astFor.Init);
                    if (astFor.Condition != null) astFor.Condition = InstrumentCondition(astFor.Condition);
                    if (astFor.Step != null) astFor.Step = Transform(astFor.Step);
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
                    _owner.StatementInfos.Add(new InstrumentedStatementInfo
                    {
                        FileName = ii.Source,
                        Start = ii.Start,
                        End = ii.End,
                        Index = idx
                    });
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
            return true;
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
            _owner.ConditionInfos.Add(new InstrumentedConditionInfo
            {
                FileName = condition.Source,
                Start = condition.Start,
                End = condition.End,
                Index = idx
            });
            return res;
        }

        protected override AstNode? After(AstNode node, bool inList)
        {
            return null;
        }
    }

    public class CoverageInstrumentation
    {
        public StructList<InstrumentedStatementInfo> StatementInfos;
        public StructList<InstrumentedConditionInfo> ConditionInfos;
        public int LastIndex;
        public readonly string StorageName;
        public readonly string FncNameCond;
        public readonly string FncNameStatement;

        public CoverageInstrumentation(string storageName = "__c0v")
        {
            StatementInfos = new StructList<InstrumentedStatementInfo>();
            ConditionInfos = new StructList<InstrumentedConditionInfo>();
            StorageName = storageName;
            FncNameCond = storageName + "C";
            FncNameStatement = storageName + "S";
        }

        public AstToplevel Instrument(AstToplevel toplevel)
        {
            var tt = new InstrumentTreeTransformer(this);
            toplevel = (AstToplevel) tt.Transform(toplevel);
            return toplevel;
        }

        public void AddCountingHelpers(AstToplevel toplevel, string globalThis = "window")
        {
            var tla = Parser.Parse(
                $"var {StorageName}=new Uint32Array({LastIndex});{globalThis}.{StorageName}={StorageName};function {FncNameStatement}(i){{{StorageName}[i]++;}}function {FncNameCond}(r,i){{{StorageName}[i+(r?1:0)]++;return r}}");
            toplevel.Body.InsertRange(0, tla.Body);
        }
    }
}
