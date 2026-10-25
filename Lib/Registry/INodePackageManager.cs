using System.Collections.Generic;
using Lib.DiskCache;
using Lib.TSCompiler;
using Lib.Utils;
using Lib.Utils.Logger;

namespace Lib.Registry;

public interface INodePackageManager
{
    bool IsAvailable { get; }
    bool IsUsedInProject(IDirectoryCache projectDirectory, IDiskCache? dc);
    IEnumerable<PackagePathVersion> GetLockedDependencies(IDirectoryCache projectDirectory);
    // frozen means lock file must not be changed, install fails if it is not up to date with package.json
    void Install(IDirectoryCache projectDirectory, IDiskCache? dc, bool frozen = false);
    void UpgradeAll(IDirectoryCache projectDirectory, IDiskCache? dc);
    void Upgrade(IDirectoryCache projectDirectory, IDiskCache? dc, string packageName);
    void Add(IDirectoryCache projectDirectory, IDiskCache? dc, string packageName, bool devDependency = false);
}

static class ProjectNpmRc
{
    // Writes .npmrc with npm registry configured in project's package.json, if project does not have own .npmrc
    public static void EnsureRegistry(IDirectoryCache projectDirectory, IDiskCache diskCache, ILogger logger)
    {
        // Create returns null only for null directory
        var project = TSProject.Create(projectDirectory, diskCache, logger, null)!;
        project.LoadProjectJson(true, null);
        // ProjectOptions is always assigned in TSProject.Create
        var npmRegistry = project.ProjectOptions!.NpmRegistry;
        if (npmRegistry == null) return;
        if (projectDirectory.TryGetChild(".npmrc") is IFileCache) return;
        diskCache.FsAbstraction.WriteAllUtf8(PathUtils.Join(projectDirectory.FullPath, ".npmrc"),
            "registry =" + npmRegistry);
    }
}
