using Njsast.Reader;

namespace Njsast.Ast
{
    /// A function definition
    public class AstDefun : AstLambda
    {
        public AstDefun(string? source, Position startPos, Position endPos, AstSymbolDeclaration? name, ref StructList<AstNode> argNames, bool isGenerator, bool async, ref StructList<AstNode> body) : base(source, startPos, endPos, name, ref argNames, isGenerator, async, ref body)
        {
        }

        AstDefun(string? source, Position startPos, Position endPos, AstSymbolDeclaration? name, bool isGenerator, bool async) : base(source, startPos, endPos, name, isGenerator, async)
        {
        }

        public override AstNode ShallowClone()
        {
            var res = new AstDefun(Source, Start, End, Name, IsGenerator, Async);
            res.Body.AddRange(Body.AsReadOnlySpan());
            res.ArgNames.AddRange(ArgNames.AsReadOnlySpan());
            res.HasUseStrictDirective = HasUseStrictDirective;
            res.Pure = Pure;
            return res;
        }
    }
}
