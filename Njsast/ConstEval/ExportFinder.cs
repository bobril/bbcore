using Njsast.Ast;
using Njsast.Reader;

namespace Njsast.ConstEval
{
    public class ExportFinder : TreeWalker
    {
        readonly string _export;
        internal AstNode Result;
        public bool CompleteResult;

        public ExportFinder(string export)
        {
            _export = export;
        }

        protected override void Visit(AstNode node)
        {
            if (node is AstDot dot)
            {
                StopDescending();
                if (IsExports(dot.Expression) && (string) dot.Property == _export)
                {
                    var parent = Parent();
                    if (parent is AstAssign assign && assign.Operator == Operator.Assignment && assign.Left == node)
                    {
                        Result = assign.Right;
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