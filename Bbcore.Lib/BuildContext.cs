using System.Collections.Generic;
using Lib.BuildCache;
using Lib.Composition;
using Lib.DiskCache;
using Lib.ToolsDir;
using Lib.TSCompiler;

namespace Bbcore.Lib;

public class BuildContext
{
    public required ToolsDir ToolsDir { get; set; }
    public required IFsAbstraction FileSystemAbstraction { get; set; }
    public required MainBuildResult MainBuildResult { get; set; }
    public required TSProject TsProject { get; set; }
    public required BuildResult BuildResult { get; set; }
    public IList<Diagnostic> Messages { get; set; } = new List<Diagnostic>();
    public required DiskCache DiskCache { get; set; }
    public required IBuildCache BuildCache { get; set; }
    public required IDirectoryCache DirectoryCache { get; set; }
    public required BuildCtx TranspilationContext { get; set; }
    public IBundler? Bundler { get; set; }
}