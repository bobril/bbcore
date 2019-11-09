using Njsast.Ast;

namespace Njsast.AstDump
{
    public interface IAstDumpWriter
    {
        void Indent();
        void Dedent();
        void Print(AstNode node);
        void PrintProp(string name, string? value);
        void PrintProp(string name, bool value);
    }
}
