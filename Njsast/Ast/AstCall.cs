using Njsast.ConstEval;
using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// A function call expression
    public class AstCall : AstNode
    {
        /// [AstNode] expression to invoke as function
        public AstNode Expression;

        /// [AstNode*] array of arguments
        public StructList<AstNode> Args;

        public AstCall(Parser parser, Position startLoc, Position endLoc, AstNode expression,
            ref StructList<AstNode> args) : base(parser, startLoc, endLoc)
        {
            Expression = expression;
            Args.TransferFrom(ref args);
        }

        public override void Visit(TreeWalker w)
        {
            base.Visit(w);
            w.Walk(Expression);
            w.WalkList(Args);
        }

        public override void CodeGen(OutputContext output)
        {
            Expression.Print(output);
            if (this is AstNew && !output.NeedConstructorParens(this))
                return;
            if (Expression is AstCall || Expression is AstLambda)
            {
                output.AddMapping(Start);
            }

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
                || p is AstExport export && export.IsDefault && Expression is AstFunction)
                return true;

            // workaround for Safari bug https://bugs.webkit.org/show_bug.cgi?id=123506
            return Expression is AstFunction
                   && p is AstPropAccess propAccess
                   && propAccess.Expression == this
                   && output.Parent(1) is AstAssign assign
                   && assign.Left == p;
        }

        public override bool IsConstValue(IConstEvalCtx ctx = null)
        {
            if (Expression is AstSymbolRef symb)
            {
                var def = symb.Thedef;
                if (def == null || ctx == null || Args.Count != 1) return false;
                if (def.Undeclared && def.Global && def.Name == "require")
                {
                    return Args[0].IsConstValue(ctx);
                }
            }

            return false;
        }

        public override object ConstValue(IConstEvalCtx ctx = null)
        {
            if (Expression is AstSymbolRef symb)
            {
                var def = symb.Thedef;
                if (def == null || ctx == null || Args.Count != 1) return null;
                if (def.Undeclared && def.Global && def.Name == "require")
                {
                    var param = Args[0].ConstValue(ctx?.StripPathResolver());
                    if (!(param is string)) return null;
                    return ctx.ResolveRequire((string) param);
                }
            }

            return null;
        }
    }
}