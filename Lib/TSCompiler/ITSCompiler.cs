using System;
using Lib.DiskCache;

namespace Lib.TSCompiler
{
    public interface ITSCompiler: IDisposable
    {
        IDiskCache DiskCache { get; set; }
        ITSCompilerOptions CompilerOptions { get; set; }
        string GetTSVersion();

        TranspileResult Transpile(string fileName, string content);

        void CreateProgram(string currentDirectory, string[] mainFiles);
        void UpdateProgram(string[] mainFiles);
        void TriggerUpdate();
        void ClearDiagnostics();
        Diagnostic[] GetDiagnostics();
        void CheckProgram(string currentDirectory, string[] mainFiles);
    }
}
