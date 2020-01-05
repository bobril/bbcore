using BTDB.Collections;

namespace Lib.TSCompiler
{
    public interface IBundler
    {
        void Build(bool compress, bool mangle, bool beautify, bool buildSourceMap, string? sourceMapSourceRoot);
    }
}
