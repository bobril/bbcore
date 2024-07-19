using System.Collections.Generic;
using Lib.DiskCache;

namespace Lib.Registry;

public interface INodePackageManager
{
    bool IsAvailable { get; }
    bool IsUsedInProject(IDirectoryCache projectDirectory, IDiskCache? dc);
    IEnumerable<PackagePathVersion> GetLockedDependencies(IDirectoryCache projectDirectory);
    void Install(IDirectoryCache projectDirectory, IDiskCache? dc);
    void UpgradeAll(IDirectoryCache projectDirectory, IDiskCache? dc);
    void Upgrade(IDirectoryCache projectDirectory, IDiskCache? dc, string packageName);
    void Add(IDirectoryCache projectDirectory, IDiskCache? dc, string packageName, bool devDependency = false);
}