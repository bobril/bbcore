namespace Lib.TSCompiler
{
    public interface ITSCompilerCtx
    {
        void writeFile(string fileName, string data);
        string resolveLocalImport(string name, TSFileAdditionalInfo parentInfo);
        string resolveModuleMain(string name, TSFileAdditionalInfo parentInfo);
        void reportDiag(bool isError, int code, string text, string fileName, int startLine, int startCharacter, int endLine, int endCharacter);
    }
}
