using System.Threading.Tasks;
using Lib.TSCompiler;
using Lib.CSSProcessor;
using Lib.SCSSProcessor;

namespace Lib.Composition;

public interface ICompilerPool
{
    ITSCompiler GetTs(DiskCache.IDiskCache diskCache, ITSCompilerOptions? compilerOptions);
    void ReleaseTs(ITSCompiler value);

    ITSCompiler GetNativeTranspiler(string executable, ITSCompilerOptions compilerOptions);
    void ReleaseNativeTranspiler(ITSCompiler value);

    ICssProcessor GetCss();
    void ReleaseCss(ICssProcessor value);

    IScssProcessor GetScss();
    void ReleaseScss(IScssProcessor value);
    Task FreeMemory();
}
