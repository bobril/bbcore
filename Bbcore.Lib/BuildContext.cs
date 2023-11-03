using System.Collections.Generic;
using Lib.BuildCache;
using Lib.Composition;
using Lib.DiskCache;
using Lib.ToolsDir;
using Lib.TSCompiler;

namespace Bbcore.Lib;

public class BuildContext
{
    public ToolsDir ToolsDir { get; set; }
    public IFsAbstraction FileSystemAbstraction { get; set; }
    public MainBuildResult MainBuildResult { get; set; }
    public TSProject TsProject { get; set; }
    public BuildResult BuildResult { get; set; }
    public IList<Diagnostic> Messages { get; set; } = new List<Diagnostic>();
    public DiskCache DiskCache { get; set; }
    public IBuildCache BuildCache { get; set; }
    public IDirectoryCache DirectoryCache { get; set; }
    public BuildCtx TranspilationContext { get; set; }
    public IBundler Bundler { get; set; }
}