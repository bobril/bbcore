using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Njsast
{
    public struct StructList<T> : IEnumerable<T>
    {
        T[] _a;
        uint _count;

        public StructList(in StructList<T> from) : this()
        {
            if (from.Count > 0)
            {
                _count = from.Count;
                _a = new T[_count];
                Array.Copy(from._a, _a, _count);
            }
        }

        public void Add(in T value)
        {
            if (_a == null || _count == _a.Length)
            {
                Expand();
            }

            _a[_count++] = value;
        }

        /// <summary>
        /// Adds value to a collection only if it is not already contained in collection
        /// </summary>
        /// <param name="value"></param>
        public bool AddUnique(in T value)
        {
            for (var i = 0u; i < _count; i++)
            {
                if (value.Equals(_a[i]))
                {
                    return false;
                }
            }

            Add(value);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T AddRef()
        {
            if (_a == null || _count == _a.Length)
            {
                Expand();
            }

            return ref _a[_count++];
        }

        public ref T Insert(uint index)
        {
            if (index > _count) throw new ArgumentOutOfRangeException(nameof(index), index, "Insert out of range");
            if (_a == null || _count == _a.Length)
            {
                Expand();
            }

            _count++;
            if (index + 1 < _count)
            {
                AsSpan((int)index, (int)_count - (int)index - 1).CopyTo(AsSpan((int)index + 1, (int)(_count - index - 1)));
            }
            _a[index] = default;
            return ref _a[index];
        }

        public void RemoveAt(uint index)
        {
            if (index >= _count) throw new ArgumentOutOfRangeException(nameof(index), index, "RemoveAt out of range");
            AsSpan((int)index + 1).CopyTo(AsSpan((int)index));
            _count--;
            _a[_count] = default;
        }

        public void Reserve(uint count)
        {
            if (count > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(count));
            if (count <= _count) return;
            Array.Resize(ref _a, (int)count);
        }

        public void Truncate()
        {
            if (_count == 0)
            {
                _a = null;
            }
            else
            {
                Array.Resize(ref _a, (int)_count);
            }
        }

        void Expand()
        {
            Array.Resize(ref _a, (int)Math.Min(int.MaxValue, Math.Max(2u, _count * 2)));
        }

        void Expand(uint count)
        {
            Array.Resize(ref _a, (int)Math.Min(int.MaxValue, Math.Max(count, _count * 2)));
        }

        public ref T this[uint index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (index >= _count)
                    ThrowIndexOutOfRange(index);
                return ref _a[index];
            }
        }

        void ThrowIndexOutOfRange(uint index)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index,
                "List has " + _count + " items. Accessing " + index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            _count = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearAndTruncate()
        {
            _count = 0;
            _a = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Pop()
        {
            if (_count == 0)
            {
                ThrowEmptyList();
            }

            _count--;
        }

        static void ThrowEmptyList()
        {
            throw new InvalidOperationException("Cannot pop empty List");
        }

        public uint Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> AsSpan()
        {
            return _a.AsSpan(0, (int)_count);
        }

        public Span<T> AsSpan(int start)
        {
            return AsSpan().Slice(start);
        }

        public Span<T> AsSpan(int start, int length)
        {
            return AsSpan().Slice(start, length);
        }

        public struct Enumerator : IEnumerator<T>
        {
            int _position;
            int _count;
            T[] _array;

            public Enumerator(int count, T[] array)
            {
                _position = -1;
                _count = count;
                _array = array;
            }

            public bool MoveNext()
            {
                Debug.Assert(_position < _count);
                _position++;
                return _position < _count;
            }

            public void Reset()
            {
                _position = -1;
            }

            public T Current => _array[_position];

            object IEnumerator.Current => Current;

            public void Dispose()
            {
            }
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator((int)_count, _a);
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return new Enumerator((int)_count, _a);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void TransferFrom(ref StructList<T> reference)
        {
            _a = reference._a;
            _count = reference._count;
            reference._a = null;
            reference._count = 0;
        }

        public void RepeatAdd(in T value, uint count)
        {
            if (count == 0) return;
            var totalCount = _count + count;
            if (_a == null || totalCount > _a.Length)
                Expand(totalCount);
            _a.AsSpan((int)_count, (int)count).Fill(value);
            _count = totalCount;
        }

        public void AddRange(in ReadOnlySpan<T> range)
        {
            if (range.IsEmpty)
                return;
            var count = _count + (uint)range.Length;
            if (_a == null || count > _a.Length)
                Expand(count);
            range.CopyTo(_a.AsSpan((int)_count));
            _count = count;
        }

        public bool All(Predicate<T> predicate)
        {
            for (uint i = 0; i < _count; i++)
            {
                if (!predicate(_a[i]))
                    return false;
            }

            return true;
        }

        public int IndexOf(in T value)
        {
            var comparer = EqualityComparer<T>.Default;
            for (uint i = 0; i < _count; i++)
            {
                if (comparer.Equals(_a[i], value))
                    return (int)i;
            }

            return -1;
        }
    }
}