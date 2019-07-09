using Lib.TSCompiler;
using Lib.CSSProcessor;

namespace Lib.Composition
{
    public interface ICompilerPool
    {
        ITSCompiler GetTs(DiskCache.IDiskCache diskCache, ITSCompilerOptions compilerOptions);
        void ReleaseTs(ITSCompiler value);

        ICssProcessor GetCss();
        void ReleaseCss(ICssProcessor value);
    }
}
