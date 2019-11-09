using Njsast.Ast;
using Njsast.Reader;
using Njsast.Runtime;

namespace Njsast.ConstEval
{
    public class ExportFinder : TreeWalker
    {
        readonly string _export;
        readonly IConstEvalCtx _ctx;
        internal AstNode? Result;
        public bool CompleteResult;

        public ExportFinder(string export, IConstEvalCtx ctx)
        {
            _export = export;
            _ctx = ctx;
        }

        protected override void Visit(AstNode node)
        {
            if (node is AstDot dot)
            {
                StopDescending();
                if (IsExports(dot.Expression) && (string)dot.Property == _export)
                {
                    var parent = Parent();
                    if (parent is AstAssign assign && assign.Operator == Operator.Assignment && assign.Left == node)
                    {
                        Result = assign.Right;
                        CompleteResult = false;
                    }
                }
            }

            if (node is AstCall call && call.Expression is AstSymbol symbol && symbol.Name == "__export" && call.Args.Count == 1 && call.Args[0] is AstCall)
            {
                var module = call.Args[0].ConstValue(_ctx);
                if (module is JsModule)
                {
                    var res = _ctx.ConstValue(_ctx, (JsModule)module, _export);
                    if (res != null)
                    {
                        Result = TypeConverter.ToAst(res);
                        CompleteResult = false;
                    }
                }
            }

            if (node is AstAssign assign2 && assign2.Operator == Operator.Assignment && IsExports(assign2.Left))
            {
                StopDescending();
                Result = assign2.Right;
                CompleteResult = true;
            }
        }

        static bool IsExports(AstNode node)
        {
            if (node is AstDot dot && dot.Property as string == "exports")
            {
                if (dot.Expression is AstSymbol symbolModule)
                {
                    var def2 = symbolModule.Thedef;
                    if (def2 == null) return false;
                    return def2.Undeclared && def2.Global && def2.Name == "module";
                }

                return false;
            }

            if (!(node is AstSymbolRef symbol))
                return false;
            var def = symbol.Thedef;
            if (def == null) return false;
            return def.Undeclared && def.Global && def.Name == "exports";
        }
    }
}