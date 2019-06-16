using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// An ES6 Arrow function ((a) => b)
    public class AstArrow : AstLambda
    {
        public bool Inlined;

        public AstArrow(Parser parser, Position startPos, Position endPos, AstSymbolDeclaration name,
            ref StructList<AstNode> argNames, bool isGenerator, bool async, ref StructList<AstNode> body) : base(parser,
            startPos, endPos, name, ref argNames, isGenerator, async, ref body)
        {
        }

        public override void DoPrint(OutputContext output, bool nokeyword = false)
        {
            var parent = output.Parent();
            var needsParens = parent is AstBinary ||
                               parent is AstUnary ||
                               (parent is AstCall call && this == call.Expression);
            if (needsParens)
                output.Print("(");
            if (Async)
            {
                output.Print("async");
                output.Space();
            }

            if (ArgNames.Count == 1 && ArgNames[0] is AstSymbol)
            {
                ArgNames[0].Print(output);
            }
            else
            {
                output.Print("(");
                for (var i = 0u; i < ArgNames.Count; i++)
                {
                    if (i > 0)
                        output.Comma();
                    ArgNames[i].Print(output);
                }

                output.Print(")");
            }

            output.Space();
            output.Print("=>");
            output.Space();
            output.PrintBraced(Body, false);
            if (needsParens)
                output.Print(")");
        }

        public override bool NeedParens(OutputContext output)
        {
            var p = output.Parent();
            return p is AstPropAccess propAccess && propAccess.Expression == this;
        }
    }
}
