using System.Collections.Generic;
using System.Linq;
using Lib.DiskCache;
using Lib.Utils.Logger;

namespace Lib.Registry
{
    class CurrentNodePackageManager : INodePackageManager
    {
        YarnNodePackageManager _yarn;
        NpmNodePackageManager _npm;

        public CurrentNodePackageManager(IDiskCache diskCache, ILogger logger)
        {
            _yarn = new YarnNodePackageManager(diskCache, logger);
            _npm = new NpmNodePackageManager(diskCache, logger);
        }

        public bool IsAvailable
        {
            get => _yarn.IsAvailable || _npm.IsAvailable;
        }

        public bool IsUsedInProject(IDirectoryCache projectDirectory)
        {
            if (_npm.IsUsedInProject(projectDirectory))
            {
                return true;
            }

            if (_yarn.IsUsedInProject(projectDirectory))
            {
                return true;
            }

            return IsAvailable;
        }

        public IEnumerable<PackagePathVersion> GetLockedDependencies(IDirectoryCache projectDirectory)
        {
            if (_npm.IsUsedInProject(projectDirectory))
            {
                return _npm.GetLockedDependencies(projectDirectory);
            }

            if (_yarn.IsUsedInProject(projectDirectory))
            {
                return _yarn.GetLockedDependencies(projectDirectory);
            }

            return Enumerable.Empty<PackagePathVersion>();
        }

        public void Install(IDirectoryCache projectDirectory)
        {
            if (_npm.IsUsedInProject(projectDirectory))
            {
                _npm.Install(projectDirectory);
                return;
            }

            if (_yarn.IsUsedInProject(projectDirectory) || _yarn.IsAvailable)
            {
                _yarn.Install(projectDirectory);
            }

            _npm.Install(projectDirectory);
        }

        public void UpgradeAll(IDirectoryCache projectDirectory)
        {
            if (_npm.IsUsedInProject(projectDirectory))
            {
                _npm.UpgradeAll(projectDirectory);
                return;
            }

            if (_yarn.IsUsedInProject(projectDirectory) || _yarn.IsAvailable)
            {
                _yarn.UpgradeAll(projectDirectory);
            }

            _npm.UpgradeAll(projectDirectory);
        }

        public void Upgrade(IDirectoryCache projectDirectory, string packageName)
        {
            if (_npm.IsUsedInProject(projectDirectory))
            {
                _npm.Upgrade(projectDirectory, packageName);
                return;
            }

            if (_yarn.IsUsedInProject(projectDirectory) || _yarn.IsAvailable)
            {
                _yarn.Upgrade(projectDirectory, packageName);
            }

            _npm.Upgrade(projectDirectory, packageName);
        }

        public void Add(IDirectoryCache projectDirectory, string packageName, bool devDependency = false)
        {
            if (_npm.IsUsedInProject(projectDirectory))
            {
                _npm.Add(projectDirectory, packageName, devDependency);
                return;
            }

            if (_yarn.IsUsedInProject(projectDirectory) || _yarn.IsAvailable)
            {
                _yarn.Add(projectDirectory, packageName, devDependency);
            }

            _npm.Add(projectDirectory, packageName, devDependency);
        }
    }
}
