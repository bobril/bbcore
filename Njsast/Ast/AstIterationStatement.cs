using Njsast.Reader;

namespace Njsast.Ast
{
    /// All loops inherit from it.
    public abstract class AstIterationStatement : AstStatementWithBody, IMayBeBlockScope
    {
        protected AstIterationStatement(string? source, Position startPos, Position endPos, AstStatement body)
            : base(source, startPos, endPos, body)
        {
        }

        public bool IsBlockScope => true;

        public AstScope? BlockScope { get; set; }

        public bool HasBreak { get; set; }
        public bool HasContinue { get; set; }
    }
}
