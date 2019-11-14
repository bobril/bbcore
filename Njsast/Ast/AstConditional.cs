using Njsast.ConstEval;
using Njsast.Output;
using Njsast.Reader;
using Njsast.Runtime;

namespace Njsast.Ast
{
    /// Conditional expression using the ternary operator, i.e. `a ? b : c`
    public class AstConditional : AstNode
    {
        public AstNode Condition;
        public AstNode Consequent;
        public AstNode Alternative;

        public AstConditional(string? source, Position startLoc, Position endLoc, AstNode condition, AstNode consequent,
            AstNode alternative) : base(source, startLoc, endLoc)
        {
            Condition = condition;
            Consequent = consequent;
            Alternative = alternative;
        }

        public override void Visit(TreeWalker w)
        {
            base.Visit(w);
            w.Walk(Condition);
            w.Walk(Consequent);
            w.Walk(Alternative);
        }

        public override void Transform(TreeTransformer tt)
        {
            base.Transform(tt);
            Condition = tt.Transform(Condition)!;
            Consequent = tt.Transform(Consequent)!;
            Alternative = tt.Transform(Alternative)!;
        }

        public override AstNode ShallowClone()
        {
            return new AstConditional(Source, Start, End, Condition, Consequent, Alternative);
        }

        public override void CodeGen(OutputContext output)
        {
            Condition.Print(output);
            output.Space();
            output.Print("?");
            output.Space();
            Consequent.Print(output);
            output.Space();
            output.Colon();
            Alternative.Print(output);
        }

        public override bool NeedParens(OutputContext output)
        {
            var p = output.Parent();
            // !(a = false) → true
            if (p is AstUnary)
                return true;
            // 1 + (a = 2) + 3 → 6, side effect setting a = 2
            if (p is AstBinary && !(p is AstAssign))
                return true;
            // (a = func)() —or— new (a = Object)()
            if (p is AstCall call && call.Expression == this)
                return true;
            // (a = foo) ? bar : baz
            if (p is AstConditional conditional && conditional.Condition == this)
                return true;
            // (a = foo)["prop"] —or— (a = foo).prop
            if (p is AstPropAccess propAccess && propAccess.Expression == this)
                return true;
            return false;
        }

        public override object? ConstValue(IConstEvalCtx? ctx = null)
        {
            var cond = Condition.ConstValue(ctx?.StripPathResolver());
            if (cond == null) return null;
            return TypeConverter.ToBoolean(cond) ? Consequent.ConstValue(ctx) : Alternative.ConstValue(ctx);
        }
    }
}
