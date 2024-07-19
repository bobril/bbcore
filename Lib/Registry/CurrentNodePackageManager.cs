using System.Collections.Generic;
using System.Linq;
using Lib.DiskCache;
using Lib.Utils.Logger;

namespace Lib.Registry;

class CurrentNodePackageManager : INodePackageManager
{
    readonly ILogger _logger;
    PnpmNodePackageManager _pnpm;
    YarnNodePackageManager _yarn;
    NpmNodePackageManager _npm;

    public CurrentNodePackageManager(IDiskCache diskCache, ILogger logger)
    {
        _logger = logger;
        _pnpm = new PnpmNodePackageManager(diskCache, logger);
        _yarn = new YarnNodePackageManager(diskCache, logger);
        _npm = new NpmNodePackageManager(diskCache, logger);
    }

    public bool IsAvailable => _pnpm.IsAvailable || _yarn.IsAvailable || _npm.IsAvailable;

    public bool IsUsedInProject(IDirectoryCache projectDirectory, IDiskCache? dc)
    {
        if (_pnpm.IsUsedInProject(projectDirectory, dc))
        {
            return true;
        }

        if (_npm.IsUsedInProject(projectDirectory, dc))
        {
            return true;
        }

        if (_yarn.IsUsedInProject(projectDirectory, dc))
        {
            return true;
        }

        return IsAvailable;
    }

    public IEnumerable<PackagePathVersion> GetLockedDependencies(IDirectoryCache projectDirectory)
    {
        if (_pnpm.IsUsedInProject(projectDirectory, null))
        {
            return _pnpm.GetLockedDependencies(projectDirectory);
        }

        if (_npm.IsUsedInProject(projectDirectory, null))
        {
            return _npm.GetLockedDependencies(projectDirectory);
        }

        if (_yarn.IsUsedInProject(projectDirectory, null))
        {
            return _yarn.GetLockedDependencies(projectDirectory);
        }

        return Enumerable.Empty<PackagePathVersion>();
    }

    INodePackageManager? Choose(IDirectoryCache projectDirectory, IDiskCache? dc)
    {
        var npmIsUsed = _npm.IsUsedInProject(projectDirectory, dc);
        var pnpmIsUsed = _pnpm.IsUsedInProject(projectDirectory, dc);
        var yarnIsUsed = _yarn.IsUsedInProject(projectDirectory, dc);

        if (npmIsUsed)
        {
            if (yarnIsUsed)
            {
                _logger.Error("Both package-lock.json and yarn.lock found. Skipping ...");
                return null;
            }

            if (pnpmIsUsed)
            {
                _logger.Error("Both package-lock.json and pnpm-lock.yaml found. Skipping ...");
                return null;
            }

            if (_npm.IsAvailable)
            {
                return _npm;
            }

            _logger.Error("Npm is used in project, but it is not found installed in PATH. Skipping ...");
            return null;
        }

        if (pnpmIsUsed || !yarnIsUsed && _pnpm.IsAvailable)
        {
            if (pnpmIsUsed && yarnIsUsed)
            {
                _logger.Error("Both pnpm-lock.yaml and yarn.lock found. Skipping ...");
                return null;
            }

            if (!pnpmIsUsed)
            {
                _logger.Info("Introducing Pnpm into project");
                return _pnpm;
            }

            if (_pnpm.IsAvailable)
            {
                return _pnpm;
            }

            _logger.Error("Pnpm is used in project, but it is not found installed in PATH. Skipping ...");
            return null;
        }

        if (yarnIsUsed || _yarn.IsAvailable)
        {
            if (!yarnIsUsed)
            {
                _logger.Info("Introducing Yarn into project");
                return _yarn;
            }

            if (_yarn.IsAvailable)
            {
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

        _logger.Error("Pnpm, Yarn and Npm are not found installed in PATH. Skipping package manager operation.");
        return null;
    }

    public void Install(IDirectoryCache projectDirectory, IDiskCache? dc)
    {
        Choose(projectDirectory, dc)?.Install(projectDirectory, dc);
    }

    public void UpgradeAll(IDirectoryCache projectDirectory, IDiskCache? dc)
    {
        Choose(projectDirectory, dc)?.UpgradeAll(projectDirectory, dc);
    }

    public void Upgrade(IDirectoryCache projectDirectory, IDiskCache? dc, string packageName)
    {
        Choose(projectDirectory, dc)?.Upgrade(projectDirectory, dc, packageName);
    }

    public void Add(IDirectoryCache projectDirectory, IDiskCache? dc, string packageName, bool devDependency = false)
    {
        Choose(projectDirectory, dc)?.Add(projectDirectory, dc, packageName, devDependency);
    }
}