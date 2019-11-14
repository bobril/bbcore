using Njsast.Reader;

namespace Njsast.Ast
{
    /// A setter/getter function.  The `name` property is always null.
    public class AstAccessor : AstLambda
    {
        public AstAccessor(string? source, Position startPos, Position endPos, ref StructList<AstNode> argNames, bool isGenerator, bool async, ref StructList<AstNode> body) : base(source, startPos, endPos, null, ref argNames, isGenerator, async, ref body)
        {
        }

        AstAccessor(string? source, Position startPos, Position endPos, AstSymbolDeclaration? name, bool isGenerator, bool async) : base(source, startPos, endPos, name, isGenerator, async)
        {
        }

        public override AstNode ShallowClone()
        {
            var res = new AstAccessor(Source, Start, End, Name, IsGenerator, Async);
            res.Body.AddRange(Body.AsReadOnlySpan());
            res.ArgNames.AddRange(ArgNames.AsReadOnlySpan());
            res.HasUseStrictDirective = HasUseStrictDirective;
            res.Pure = Pure;
            return res;
        }
    }
}
