using Lib.DiskCache;
using System.Collections.Generic;

namespace Lib.TSCompiler
{
    public interface ITSCompilerCtx
    {
        string resolveLocalImport(string name, TSFileAdditionalInfo parentInfo);
        string resolveModuleMain(string name, TSFileAdditionalInfo parentInfo);
        void reportDiag(bool isError, int code, string text, string fileName, int startLine, int startCharacter, int endLine, int endCharacter);
        string readFile(string fullPath);
        IFileCache TryGetFile(string fullPath);
    }
}
