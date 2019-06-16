using Njsast.AstDump;
using Njsast.ConstEval;
using Njsast.Output;
using Njsast.Reader;

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
        public string Source;

        protected AstNode(Parser parser, Position startLoc, Position endLoc)
        {
            Source = parser?.SourceFile;
            Start = startLoc;
            End = endLoc;
        }

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

        public virtual void Visit(TreeWalker w)
        {
        }

        public virtual void DumpScalars(IAstDumpWriter writer)
        {
        }

        public abstract void CodeGen(OutputContext output);

        public virtual bool NeedParens(OutputContext output)
        {
            return false;
        }

        public void Print(OutputContext output, bool forceParens = false)
        {
            output.PushNode(this);
            if (forceParens || !output.HasParens() && NeedParens(output))
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

        public string PrintToString(OutputOptions options = null)
        {
            var o = new OutputContext(options);
            Print(o);
            return o.ToString();
        }

        /// Optimistic test if this AST Tree is constant expression
        public virtual bool IsConstValue(IConstEvalCtx ctx = null)
        {
            return false;
        }

        /// Returns null if not constant
        public virtual object ConstValue(IConstEvalCtx ctx = null)
        {
            return null;
        }
    }
}
