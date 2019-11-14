using Njsast.Reader;

namespace Njsast.Ast
{
    /// A class definition
    public class AstDefClass : AstClass
    {
        public AstDefClass(string? source, Position startPos, Position endPos, AstSymbolDeclaration name, AstNode extends, ref StructList<AstObjectProperty> properties) : base(source, startPos, endPos, name, extends, ref properties)
        {
        }
    }
}
