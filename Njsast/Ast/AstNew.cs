using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// An object instantiation.  Derives from a function call since it has exactly the same properties
    public class AstNew : AstCall
    {
        public AstNew(string? source, Position startLoc, Position endLoc, AstNode expression,
            ref StructList<AstNode> args) : base(source, startLoc, endLoc, expression, ref args)
        {
        }

        AstNew(string? source, Position startLoc, Position endLoc, AstNode expression) : base(source, startLoc, endLoc, expression)
        {
        }

        public override AstNode ShallowClone()
        {
            var res = new AstNew(Source, Start, End, Expression);
            res.Args.AddRange(Args.AsReadOnlySpan());
            return res;
        }

        public override void CodeGen(OutputContext output)
        {
            output.Print("new");
            output.Space();
            base.CodeGen(output);
        }

        public override bool NeedParens(OutputContext output)
        {
            var p = output.Parent();
            return !output.NeedConstructorParens(this)
                   && (p is AstPropAccess // (new Date).getTime(), (new Date)["getTime"]()
                       || p is AstCall call && call.Expression == this); // (new foo)(bar)
        }
    }
}
