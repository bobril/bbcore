using Lib.TSCompiler;
using Lib.CSSProcessor;
using Lib.SCSSProcessor;

namespace Lib.Composition;

public interface ICompilerPool
{
    ITSCompiler GetTs(DiskCache.IDiskCache diskCache, ITSCompilerOptions? compilerOptions);
    void ReleaseTs(ITSCompiler value);

    ICssProcessor GetCss();
    void ReleaseCss(ICssProcessor value);

    IScssProcessor GetScss();
    void ReleaseScss(IScssProcessor value);
}
