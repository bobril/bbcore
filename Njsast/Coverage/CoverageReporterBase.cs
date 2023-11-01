using Shared.DiskCache;

namespace Njsast.Coverage;

public class CoverageReporterBase
{
    protected readonly CoverageInstrumentation _covInstr;
    protected readonly IFsAbstraction _fs;

    public CoverageReporterBase(CoverageInstrumentation covInstr, IFsAbstraction fs)
    {
        _covInstr = covInstr;
        _fs = fs;
    }

    public virtual void Run()
    {
        var root = _covInstr.DirectoryStats[""];
        OnStartRoot(root);
        RecursiveVisit(root);
        OnFinishRoot(root);
    }

    public void RecursiveVisit(CoverageStats stats)
    {
        foreach (var subDirectory in stats.SubDirectories)
        {
            OnStartDirectory(subDirectory);
            RecursiveVisit(subDirectory);
            OnFinishDirectory(subDirectory);
        }

        foreach (var subFile in stats.SubFiles)
        {
            OnStartFile(subFile);
            OnFinishFile(subFile);
        }
    }

    public virtual void OnStartRoot(CoverageStats stats)
    {
    }

    public virtual void OnFinishRoot(CoverageStats stats)
    {
    }

    public virtual void OnStartDirectory(CoverageStats stats)
    {
    }

    public virtual void OnFinishDirectory(CoverageStats stats)
    {
    }

    public virtual void OnStartFile(CoverageFile file)
    {
    }

    public virtual void OnFinishFile(CoverageFile file)
    {
    }
}