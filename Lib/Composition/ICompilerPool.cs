using Lib.TSCompiler;

namespace Lib.Composition;

public interface ICompilerPool
{
    ITSCompiler GetTs(DiskCache.IDiskCache diskCache, ITSCompilerOptions? compilerOptions);
    void ReleaseTs(ITSCompiler value);
}
