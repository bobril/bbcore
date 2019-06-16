namespace Lib.DiskCache
{
    public interface IItemCache
    {
        string Name { get; }
        string FullPath { get; }
        IDirectoryCache Parent { get; }
        int ChangeId { get; }
        bool IsFile { get; }
        bool IsDirectory { get; }
        bool IsInvalid { get; set; }
        bool IsStale { get; set; }
    }
}