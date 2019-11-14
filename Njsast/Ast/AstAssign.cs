using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// An assignment expression — `a = b + 5`
    public class AstAssign : AstBinary
    {
        public AstAssign(string? source, Position startLoc, Position endLoc, AstNode left, AstNode right, Operator op) :
            base(source, startLoc, endLoc, left, right, op)
        {
        }

        public AstAssign(AstNode left, AstNode right, Operator op = Operator.Assignment) : base(left, right, op)
        {
        }

        public override AstNode ShallowClone()
        {
            return new AstAssign(Source, Start, End, Left, Right, Operator);
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
            // ({a, b} = {a: 1, b: 2}), a destructuring assignment
            if (Left is AstDestructuring leftDest && leftDest.IsArray == false)
                return true;
            return false;
        }
    }
}
