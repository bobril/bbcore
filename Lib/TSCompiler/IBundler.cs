using BTDB.Collections;

namespace Lib.TSCompiler
{
    public interface IBundler
    {
        // value could be string or byte[] or Lazy<string|byte[]>
        RefDictionary<string, object> FilesContent { set; }
        ProjectOptions Project { set; }
        BuildResult BuildResult { set; }
        bool BuildSourceMap { set; }
        string? SourceMapSourceRoot { set; }
        void Build(bool compress, bool mangle, bool beautify);
    }
}
