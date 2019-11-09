namespace Njsast.Ast
{
    public interface IMayBeBlockScope
    {
        bool IsBlockScope { get; }
        AstScope? BlockScope { get; set; }
    }
}
