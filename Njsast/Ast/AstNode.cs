using Njsast.AstDump;
using Njsast.ConstEval;
using Njsast.Output;
using Njsast.Reader;
using Njsast.SourceMap;

namespace Njsast.Ast
{
    /// Base class of all AST nodes
    public abstract class AstNode
    {
        /// Start position in Source
        public Position Start;

        /// End position in Source
        public Position End;

        /// Name of original Source Code
        public string? Source;

        protected AstNode(AstNode node)
        {
            Source = node.Source;
            Start = node.Start;
            End = node.End;
        }

        protected AstNode()
        {
            Source = null;
            Start = new Position();
            End = new Position();
        }

        protected AstNode(Position startLoc, Position endLoc)
        {
            Start = startLoc;
            End = endLoc;
        }

        protected AstNode(string? source, Position startPos, Position endPos)
        {
            Source = source;
            Start = startPos;
            End = endPos;
        }

        public virtual void Visit(TreeWalker w)
        {
        }

        public virtual void Transform(TreeTransformer tt)
        {
        }

        public virtual void DumpScalars(IAstDumpWriter writer)
        {
        }

        public abstract AstNode ShallowClone();

        public abstract void CodeGen(OutputContext output);

        public virtual bool NeedParens(OutputContext output)
        {
            return false;
        }

        public void Print(OutputContext output, bool forceParens = false)
        {
            output.PushNode(this);
            if (this is AstToplevel)
                output.AddMapping(null, new Position(), true);
            else
                output.AddMapping(Source, Start, true);

            if (forceParens || NeedParens(output))
            {
                output.Print("(");
                CodeGen(output);
                output.Print(")");
            }
            else
            {
                CodeGen(output);
            }

            output.PopNode();
        }

        public string PrintToString(OutputOptions? options = null)
        {
            var o = new OutputContext(options);
            Print(o);
            return o.ToString();
        }

        public void PrintToBuilder(SourceMapBuilder builder, OutputOptions? options = null)
        {
            var o = new OutputContext(options, builder);
            Print(o);
            builder.AddMapping(null, 0, 0, false);
        }

        /// Returns null if not constant
        public virtual object? ConstValue(IConstEvalCtx? ctx = null)
        {
            return null;
        }
    }
}
