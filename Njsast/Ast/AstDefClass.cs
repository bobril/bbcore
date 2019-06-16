using Njsast.Reader;

namespace Njsast.Ast
{
    /// A class definition
    public class AstDefClass : AstClass
    {
        public AstDefClass(Parser parser, Position startPos, Position endPos, AstSymbolDeclaration name, AstNode extends, ref StructList<AstObjectProperty> properties) : base(parser, startPos, endPos, name, extends, ref properties)
        {
        }
    }
}
