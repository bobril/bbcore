using Njsast.AstDump;
using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// Base class for functions
    public abstract class AstLambda : AstScope
    {
        /// [AstSymbolDeclaration?] the name of this function
        public AstSymbolDeclaration? Name;

        /// [AstSymbolFunarg|AstDestructuring|AstExpansion|AstDefaultAssign*] array of function arguments, destructurings, or expanding arguments
        public StructList<AstNode> ArgNames;

        /// [boolean/S] tells whether this function accesses the arguments array
        public bool UsesArguments;

        /// [boolean] is this a generator method
        public bool IsGenerator;

        /// [boolean] is this method async
        public bool Async;

        /// Calling this function does not have visible side effect when its result is not used (null means unknown)
        public bool? Pure;

        protected AstLambda(string? source, Position startPos, Position endPos, AstSymbolDeclaration? name,
            ref StructList<AstNode> argNames, bool isGenerator, bool async, ref StructList<AstNode> body) : base(source,
            startPos, endPos)
        {
            Name = name;
            ArgNames.TransferFrom(ref argNames);
            IsGenerator = isGenerator;
            Async = async;
            Body.TransferFrom(ref body);
        }

        protected AstLambda(string? source, Position startPos, Position endPos, AstSymbolDeclaration? name, bool isGenerator, bool async) : base(source,
            startPos, endPos)
        {
            Name = name;
            IsGenerator = isGenerator;
            Async = async;
        }

        protected AstLambda()
        {
        }

        public override void Visit(TreeWalker w)
        {
            w.Walk(Name);
            w.WalkList(ArgNames);
            base.Visit(w);
        }

        public override void Transform(TreeTransformer tt)
        {
            if (Name != null)
                Name = (AstSymbolDeclaration)tt.Transform(Name);
            tt.TransformList(ref ArgNames);
            base.Transform(tt);
        }

        public override void DumpScalars(IAstDumpWriter writer)
        {
            base.DumpScalars(writer);
            writer.PrintProp("IsGenerator", IsGenerator);
            writer.PrintProp("Async", Async);
            writer.PrintProp("Pure", Pure ?? false);
            writer.PrintProp("Impure", !Pure ?? false);
        }

        public override void InitScopeVars(AstScope? parentScope)
        {
            base.InitScopeVars(parentScope);
            UsesArguments = false;
            // Arrow functions cannot use arguments
            if (!(this is AstArrow))
                DefVariable(new AstSymbolFunarg(this, "arguments"), null);
        }

        public override AstScope Resolve()
        {
            return this;
        }

        public override void CodeGen(OutputContext output)
        {
            DoPrint(output);
        }

        public virtual void DoPrint(OutputContext output, bool noKeyword = false)
        {
            if (!noKeyword)
            {
                if (Async)
                {
                    output.Print("async");
                    output.Space();
                }

                output.Print("function");
                if (IsGenerator)
                {
                    output.Print("*");
                }

                if (Name != null)
                {
                    output.Space();
                }
            }

            if (Name != null)
            {
                Name.Print(output);
            }
            else if (noKeyword && Name != null)
            {
                output.Print("[");
                Name.Print(output); // Computed method name
                output.Print("]");
            }

            output.Print("(");
            for (var i = 0; i < ArgNames.Count; i++)
            {
                if (i > 0) output.Comma();
                ArgNames[(uint) i].Print(output);
            }

            output.Print(")");
            output.Space();
            output.PrintBraced(this, HasUseStrictDirective);
        }

        public override bool IsBlockScope => false;
    }
}
