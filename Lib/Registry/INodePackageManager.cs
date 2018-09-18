using Lib.DiskCache;

namespace Lib.Registry
{
    public interface INodePackageManager
    {
        bool IsAvailable { get; }
        bool IsUsedInProject(IDirectoryCache projectDirectory);
        
    }
}
