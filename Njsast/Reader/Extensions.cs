using System.Collections.Generic;
using JetBrains.Annotations;

namespace Njsast.Reader
{
    static class Extensions
    {
        public static T Pop<T>([NotNull] this IList<T> list)
        {
            var item = list[list.Count - 1];
            list.RemoveAt(list.Count - 1);
            return item;
        }

        public static char Get(this string s, int index)
        {
            if (index < 0 || index >= s.Length)
                return '\0';
            return s[index];
        }
    }
}
