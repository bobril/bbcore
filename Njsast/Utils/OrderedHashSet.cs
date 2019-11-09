using System.Collections.ObjectModel;

namespace Njsast.Utils
{
    public class OrderedHashSet<T> : KeyedCollection<T, T>
    {
        protected override T GetKeyForItem(T item)
        {
            return item;
        }
    }
}
