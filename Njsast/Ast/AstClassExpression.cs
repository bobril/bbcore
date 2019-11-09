using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// A class expression.
    public class AstClassExpression : AstClass
    {
        public AstClassExpression(Parser parser, Position startPos, Position endPos, AstSymbolDeclaration? name,
            AstNode? extends, ref StructList<AstObjectProperty> properties) : base(parser, startPos, endPos, name,
            extends, ref properties)
        {
        }

        public override bool NeedParens(OutputContext output)
        {
            return output.FirstInStatement();
        }
    }
}
