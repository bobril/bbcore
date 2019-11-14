using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// An ES6 Arrow function ((a) => b)
    public class AstArrow : AstLambda
    {
        public AstArrow(string? source, Position startPos, Position endPos, AstSymbolDeclaration? name,
            ref StructList<AstNode> argNames, bool isGenerator, bool async, ref StructList<AstNode> body) : base(source,
            startPos, endPos, name, ref argNames, isGenerator, async, ref body)
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
            var needsParens = parent is AstBinary ||
                               parent is AstUnary ||
                               (parent is AstCall call && this == call.Expression);
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
                // We expect that only scope (function, class,...), simple statement, constants or array is valid expression
                // Invalid expressions are: AstBreak, AstCatch, AstConst, AstContinue, AstDwLoop, AstDebugger,
                // AstDefinitions, AstDo, AstEmptyStatement, AstExit, AstExport, AstFinally, AstFor, AstForIn, AstForOf,
                // AstIf, AstImport, AstIterationStatement, AstJump, AstLabeledStatement, AstLet, AstLoopControl,
                // AstReturn, AstStatementWithBody, AstSwitch, AstThrow, AstTry, AstVar, AstWhile, AstWith
                // At this level it should not be: AstAccessor, AstBlockStatement, AstCase, AstDefClass, AstDefault,
                // AstSwitchBranch, AstToplevel
                if (Body.Last is AstScope scope)
                {
                    scope.CodeGen(output);
                    return;
                }

                if (Body.Last is AstSimpleStatement simpleStatement)
                {
                    simpleStatement.Body.Print(output);
                    return;
                }

                if (Body.Last is AstConstant constant)
                {
                    constant.CodeGen(output);
                    return;
                }

                if (Body.Last is AstArray array)
                {
                    array.CodeGen(output);
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
}
