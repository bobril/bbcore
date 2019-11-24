using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// A variable declaration; only appears in a AST_Definitions node
    public class AstVarDef : AstNode
    {
        /// [AstDestructuring|AstSymbolConst|AstSymbolLet|AstSymbolVar] name of the variable
        public AstNode Name;

        /// [AstNode?] initializer, or null of there's no initializer
        public AstNode? Value;

        public AstVarDef(string? source, Position startLoc, Position endLoc, AstNode name, AstNode? value = null) : base(
            source, startLoc, endLoc)
        {
            Name = name;
            Value = value;
        }

        public AstVarDef(AstNode name, AstNode? value = null)
        {
            Name = name;
            Value = value;
        }

        public AstVarDef(AstNode from, AstNode name, AstNode? value = null): base(from)
        {
            Name = name;
            Value = value;
        }

        public override void Visit(TreeWalker w)
        {
            base.Visit(w);
            w.Walk(Name);
            w.Walk(Value);
        }

        public override void Transform(TreeTransformer tt)
        {
            base.Transform(tt);
            Name = tt.Transform(Name);
            if (Value != null)
            {
                Value = tt.Transform(Value);
                if (Value == TreeTransformer.Remove)
                    Value = null;
            }
        }

        public override AstNode ShallowClone()
        {
            return new AstVarDef(Source, Start, End, Name, Value);
        }

        public override void CodeGen(OutputContext output)
        {
            Name.Print(output);
            if (Value != null)
            {
                output.Space();
                output.Print("=");
                output.Space();
                var p = output.Parent(1);
                var noin = p is AstFor || p is AstForIn;
                output.ParenthesizeForNoIn(Value, noin);
            }
        }
    }
}
