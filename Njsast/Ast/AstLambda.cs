using Njsast.AstDump;
using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// Base class for functions
    public class AstLambda : AstScope
    {
        /// [AstSymbolDeclaration?] the name of this function
        public AstSymbolDeclaration Name;

        /// [AstSymbolFunarg|AstDestructuring|AstExpansion|AstDefaultAssign*] array of function arguments, destructurings, or expanding arguments
        public StructList<AstNode> ArgNames;

        /// [boolean/S] tells whether this function accesses the arguments array
        public bool UsesArguments;

        /// [boolean] is this a generator method
        public bool IsGenerator;

        /// [boolean] is this method async
        public bool Async;

        public AstLambda(Parser parser, Position startPos, Position endPos, AstSymbolDeclaration name,
            ref StructList<AstNode> argNames, bool isGenerator, bool async, ref StructList<AstNode> body) : base(parser,
            startPos, endPos)
        {
            Name = name;
            ArgNames.TransferFrom(ref argNames);
            IsGenerator = isGenerator;
            Async = async;
            Body.TransferFrom(ref body);
        }

        public override void Visit(TreeWalker w)
        {
            w.Walk(Name);
            w.WalkList(ArgNames);
            base.Visit(w);
        }

        public override void DumpScalars(IAstDumpWriter writer)
        {
            base.DumpScalars(writer);
            writer.PrintProp("IsGenerator", IsGenerator);
            writer.PrintProp("Async", Async);
        }

        public override void InitScopeVars(AstScope parentScope)
        {
            base.InitScopeVars(parentScope);
            UsesArguments = false;
            // Arrow functions cannot use arguments
            if (!(this is AstArrow))
                DefVariable(new AstSymbolFunarg(Start, End, "arguments"), null);
        }

        public override AstScope Resolve()
        {
            return this;
        }

        public override void CodeGen(OutputContext output)
        {
            DoPrint(output);
        }

        public virtual void DoPrint(OutputContext output, bool nokeyword = false)
        {
            if (!nokeyword)
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

            if (Name is AstSymbol)
            {
                Name.Print(output);
            }
            else if (nokeyword && Name != null)
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
