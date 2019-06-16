using Njsast.Reader;

namespace Njsast.Ast
{
    /// Internal class.  All loops inherit from it.
    public class AstIterationStatement : AstStatementWithBody, IMayBeBlockScope
    {
        public AstIterationStatement(Parser parser, Position startPos, Position endPos, AstStatement body)
            : base(parser, startPos, endPos, body)
        {
        }

        public bool IsBlockScope => true;

        public AstScope BlockScope { get; set; }
    }
}
