using System.Collections.Generic;
using System.Linq;
using Lib.DiskCache;
using Lib.Utils.Logger;

namespace Lib.Registry
{
    class CurrentNodePackageManager : INodePackageManager
    {
        readonly ILogger _logger;
        YarnNodePackageManager _yarn;
        NpmNodePackageManager _npm;

        public CurrentNodePackageManager(IDiskCache diskCache, ILogger logger)
        {
            _logger = logger;
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

        INodePackageManager Choose(IDirectoryCache projectDirectory)
        {
            if (_npm.IsUsedInProject(projectDirectory))
            {
                if (_yarn.IsUsedInProject(projectDirectory))
                {
                    _logger.Error("Both package-lock.json and yarn.lock found. Skipping ...");
                    return null;
                }
                if (_npm.IsAvailable)
                {
                    return _npm;
                }
                _logger.Error("Npm is used in project, but it is not found installed in PATH. Skipping ...");
                return null;
            }

            var yarnIsUsed = _yarn.IsUsedInProject(projectDirectory);
            if (yarnIsUsed || _yarn.IsAvailable)
            {
                if (_yarn.IsAvailable)
                {
                    if (!yarnIsUsed)
                        _logger.Info("Introducing Yarn into project");
                    return _yarn;
                }
                _logger.Error("Yarn is used in project, but it is not found installed in PATH. Skipping ...");
                return null;
            }
            if (_npm.IsAvailable)
            {
                _logger.Info("Introducing Npm into project");
                return _npm;
            }
            _logger.Error("Yarn and Npm are not found installed in PATH. Skipping package manager operation.");
            return null;
        }

        public void Install(IDirectoryCache projectDirectory)
        {
            Choose(projectDirectory)?.Install(projectDirectory);
        }

        public void UpgradeAll(IDirectoryCache projectDirectory)
        {
            Choose(projectDirectory)?.UpgradeAll(projectDirectory);
        }

        public void Upgrade(IDirectoryCache projectDirectory, string packageName)
        {
            Choose(projectDirectory)?.Upgrade(projectDirectory, packageName);
        }

        public void Add(IDirectoryCache projectDirectory, string packageName, bool devDependency = false)
        {
            Choose(projectDirectory)?.Add(projectDirectory, packageName, devDependency);
        }
    }
}
