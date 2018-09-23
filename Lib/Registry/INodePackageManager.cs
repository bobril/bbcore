using System.Collections.Generic;
using Lib.DiskCache;

namespace Lib.Registry
{
    public interface INodePackageManager
    {
        bool IsAvailable { get; }
        bool IsUsedInProject(IDirectoryCache projectDirectory);
        IEnumerable<PackagePathVersion> GetLockedDependencies(IDirectoryCache projectDirectory);
        void Install(IDirectoryCache projectDirectory);
        void UpgradeAll(IDirectoryCache projectDirectory);
        void Upgrade(IDirectoryCache projectDirectory, string packageName);
        void Add(IDirectoryCache projectDirectory, string packageName, bool devDependency = false);
    }
}
