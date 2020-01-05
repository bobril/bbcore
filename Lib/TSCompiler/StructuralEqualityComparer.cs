using System.Collections.Generic;
using System.Collections;

namespace Lib.TSCompiler
{
    public class StructuralEqualityComparer<T> : IEqualityComparer<T>
    {
        public bool Equals(T x, T y)
        {
            return StructuralComparisons.StructuralEqualityComparer.Equals(x, y);
        }

        public int GetHashCode(T obj)
        {
            return StructuralComparisons.StructuralEqualityComparer.GetHashCode(obj);
        }

        static StructuralEqualityComparer<T> defaultComparer;
        public static StructuralEqualityComparer<T> Default
        {
            get
            {
                StructuralEqualityComparer<T> comparer = defaultComparer;
                if (comparer == null)
                {
                    comparer = new StructuralEqualityComparer<T>();
                    defaultComparer = comparer;
                }
                return comparer;
            }
        }
    }
}
