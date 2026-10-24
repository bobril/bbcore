using System.Collections.ObjectModel;

namespace Lib.Utils;

public class OrderedHashSet<T> : KeyedCollection<T, T> where T : notnull
{
    protected override T GetKeyForItem(T item)
    {
        return item;
    }
}