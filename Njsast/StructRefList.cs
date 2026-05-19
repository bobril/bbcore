using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Njsast;

public struct StructRefList<T> : IEnumerable<T> where T : class
{
    object? _itemOrArray;
    uint _count;

    public StructRefList(in StructRefList<T> from) : this()
    {
        _count = from._count;
        _itemOrArray = from._count switch
        {
            0 => null,
            1 => from._itemOrArray,
            _ => from.ToArray()
        };
    }

    public StructRefList(T[] from)
    {
        _count = (uint)from.Length;
        _itemOrArray = from.Length switch
        {
            0 => null,
            1 => from[0],
            _ => from
        };
    }

    public StructRefList(in StructList<T> from)
    {
        _count = from.Count;
        _itemOrArray = from.Count switch
        {
            0 => null,
            1 => from[0],
            _ => from.ToArray()
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DebuggerStepThrough]
    public void Add(T value)
    {
        switch (_count)
        {
            case 0:
                _itemOrArray = value;
                _count = 1;
                return;
            case 1:
            {
                var array = new T[2];
                array[0] = (T)_itemOrArray!;
                array[1] = value;
                _itemOrArray = array;
                _count = 2;
                return;
            }
        }

        var a = (T[])_itemOrArray!;
        if (_count == a.Length)
        {
            Expand();
            a = (T[])_itemOrArray!;
        }

        a[_count++] = value;
    }

    public bool AddUnique(T value)
    {
        for (var i = 0u; i < _count; i++)
        {
            if (ReferenceEquals(value, this[i]) || value.Equals(this[i]))
                return false;
        }

        Add(value);
        return true;
    }

    public void Reserve(uint count)
    {
        if (count > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(count));
        if (count <= _count || count <= 1) return;
        if (_count == 0)
        {
            _itemOrArray = new T[(int)count];
            return;
        }

        if (_count == 1)
        {
            var array = new T[(int)count];
            array[0] = (T)_itemOrArray!;
            _itemOrArray = array;
            return;
        }

        var a = (T[])_itemOrArray!;
        if (count > a.Length)
            Array.Resize(ref a, (int)count);
        _itemOrArray = a;
    }

    public void Truncate()
    {
        if (_count == 0)
        {
            _itemOrArray = null;
            return;
        }

        if (_count == 1)
        {
            if (_itemOrArray is T[] a)
                _itemOrArray = a[0];
            return;
        }

        var array = (T[])_itemOrArray!;
        Array.Resize(ref array, (int)_count);
        _itemOrArray = array;
    }

    void Expand()
    {
        var array = (T[])_itemOrArray!;
        Array.Resize(ref array, (int)Math.Min(int.MaxValue, Math.Max(2u, _count * 2)));
        _itemOrArray = array;
    }

    public T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DebuggerStepThrough]
        get
        {
            if ((uint)index >= _count)
                ThrowIndexOutOfRange(index);
            return _count == 1 ? SingleItem() : ((T[])_itemOrArray!)[index];
        }
    }

    public T this[uint index] => this[(int)index];

    public T this[Index index] => this[index.GetOffset((int)_count)];

    public T Last
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DebuggerStepThrough]
        get
        {
            if (_count == 0) ThrowEmptyList();
            return this[_count - 1];
        }
    }

    public void RemoveAt(Index index)
    {
        var idx = index.GetOffset((int)_count);
        if ((uint)idx >= _count) ThrowIndexOutOfRange(idx);
        if (_count == 1)
        {
            _itemOrArray = null;
            _count = 0;
            return;
        }

        var array = (T[])_itemOrArray!;
        if (_count == 2)
        {
            _itemOrArray = array[1 - idx];
            array[0] = null!;
            array[1] = null!;
            _count = 1;
            return;
        }

        array.AsSpan(idx + 1, (int)_count - idx - 1).CopyTo(array.AsSpan(idx));
        _count--;
        array[_count] = null!;
    }

    public void RemoveItem(T item)
    {
        var index = IndexOf(item);
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(item), item, "Item not found in list");
        RemoveAt(index);
    }

    public void ReplaceItem(T originalItem, T newItem)
    {
        var index = IndexOf(originalItem);
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(originalItem), originalItem, "Item not found in list");
        SetItem(index, newItem);
    }

    public void Insert(int index, T value)
    {
        if ((uint)index > _count) ThrowIndexOutOfRange(index);
        if (_count == 0)
        {
            _itemOrArray = value;
            _count = 1;
            return;
        }

        var result = new T[_count + 1];
        if (_count == 1)
        {
            if (index == 0)
            {
                result[0] = value;
                result[1] = SingleItem();
            }
            else
            {
                result[0] = SingleItem();
                result[1] = value;
            }
        }
        else
        {
            var array = (T[])_itemOrArray!;
            array.AsSpan(0, index).CopyTo(result);
            result[index] = value;
            array.AsSpan(index, (int)_count - index).CopyTo(result.AsSpan(index + 1));
        }

        _itemOrArray = result;
        _count++;
    }

    public void Insert(uint index, T value) => Insert((int)index, value);

    public void Insert(Index index, T value) => Insert(index.GetOffset((int)_count), value);

    public void InsertRange(int index, ReadOnlySpan<T> values)
    {
        if ((uint)index > _count) ThrowIndexOutOfRange(index);
        if (values.IsEmpty) return;
        if (values.Length == 1)
        {
            Insert(index, values[0]);
            return;
        }

        var result = new T[_count + values.Length];
        if (_count == 1)
        {
            if (index == 0)
            {
                values.CopyTo(result);
                result[values.Length] = SingleItem();
            }
            else
            {
                result[0] = SingleItem();
                values.CopyTo(result.AsSpan(1));
            }
        }
        else if (_count > 1)
        {
            var array = (T[])_itemOrArray!;
            array.AsSpan(0, index).CopyTo(result);
            values.CopyTo(result.AsSpan(index));
            array.AsSpan(index, (int)_count - index).CopyTo(result.AsSpan(index + values.Length));
        }
        else
        {
            values.CopyTo(result);
        }

        _itemOrArray = result.Length == 1 ? result[0] : result;
        _count = (uint)result.Length;
    }

    public void InsertRange(uint index, ReadOnlySpan<T> values) => InsertRange((int)index, values);

    public void InsertRange(Index index, ReadOnlySpan<T> values) => InsertRange(index.GetOffset((int)_count), values);

    public void SetItem(int index, T value)
    {
        if ((uint)index >= _count)
            ThrowIndexOutOfRange(index);
        if (_count == 1)
            _itemOrArray = value;
        else
            ((T[])_itemOrArray!)[index] = value;
    }

    public void SetItem(uint index, T value)
    {
        SetItem((int)index, value);
    }

    public void SetItem(Index index, T value)
    {
        SetItem(index.GetOffset((int)_count), value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        if (_count > 1)
            Array.Clear((T[])_itemOrArray!, 0, (int)_count);
        _count = 0;
        _itemOrArray = null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ClearAndTruncate()
    {
        Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DebuggerStepThrough]
    public void Pop()
    {
        if (_count == 0)
            ThrowEmptyList();
        RemoveAt(^1);
    }

    public readonly uint Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DebuggerStepThrough]
        get => _count;
    }

    public ReadOnlySpan<T> AsReadOnlySpan()
    {
        return _count == 0 ? ReadOnlySpan<T>.Empty :
            _count == 1 ? MemoryMarshal.CreateReadOnlySpan(ref SingleItemRefForSpan(), 1) :
            ((T[])_itemOrArray!).AsSpan(0, (int)_count);
    }

    public ReadOnlySpan<T> AsReadOnlySpan(int start)
    {
        return AsReadOnlySpan().Slice(start);
    }

    public ReadOnlySpan<T> AsReadOnlySpan(int start, int length)
    {
        return AsReadOnlySpan().Slice(start, length);
    }

    public bool All(Predicate<T> predicate)
    {
        for (uint i = 0; i < _count; i++)
        {
            if (!predicate(this[i]))
                return false;
        }

        return true;
    }

    public int IndexOf(T value)
    {
        var comparer = EqualityComparer<T>.Default;
        for (uint i = 0; i < _count; i++)
        {
            if (comparer.Equals(this[i], value))
                return (int)i;
        }

        return -1;
    }

    public readonly T[] ToArray()
    {
        if (_count == 0) return [];
        if (_count == 1) return [(T)_itemOrArray!];
        var res = new T[_count];
        ((T[])_itemOrArray!).AsSpan(0, (int)_count).CopyTo(res);
        return res;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    T SingleItem()
    {
        return Unsafe.As<T>(_itemOrArray!);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    ref T SingleItemRefForSpan()
    {
        return ref Unsafe.As<object?, T>(ref Unsafe.AsRef(in _itemOrArray));
    }

    public void AddRange(ReadOnlySpan<T> range)
    {
        if (range.IsEmpty)
            return;
        if (range.Length == 1)
        {
            Add(range[0]);
            return;
        }

        var oldCount = _count;
        var totalCount = _count + (uint)range.Length;
        Reserve(totalCount);
        if (oldCount == 0)
            _count = totalCount;
        else if (oldCount == 1)
            _count = totalCount;
        else
            _count = totalCount;
        range.CopyTo(((T[])_itemOrArray!).AsSpan((int)oldCount));
    }

    public void AddRange(in StructRefList<T> range)
    {
        AddRange(range.AsReadOnlySpan());
    }

    public void TransferFrom(ref StructRefList<T> reference)
    {
        _itemOrArray = reference._itemOrArray;
        _count = reference._count;
        reference._itemOrArray = null;
        reference._count = 0;
    }

    public void TransferFrom(ref StructList<T> reference)
    {
        _count = reference.Count;
        _itemOrArray = reference.Count switch
        {
            0 => null,
            1 => reference[0],
            _ => reference.ToArray()
        };
        reference.ClearAndTruncate();
    }

    public void ReplaceItemAt(Index index, ReadOnlySpan<T> newItems)
    {
        var idx = index.GetOffset((int)_count);
        if ((uint)idx > _count) ThrowIndexOutOfRange(idx);
        if (newItems.Length == 0)
        {
            RemoveAt(idx);
            return;
        }

        if (newItems.Length == 1)
        {
            SetItem(idx, newItems[0]);
            return;
        }

        var old = ToArray();
        var result = new T[_count + newItems.Length - 1];
        old.AsSpan(0, idx).CopyTo(result);
        newItems.CopyTo(result.AsSpan(idx));
        old.AsSpan(idx + 1).CopyTo(result.AsSpan(idx + newItems.Length));
        _itemOrArray = result;
        _count = (uint)result.Length;
    }

    public readonly Enumerator GetEnumerator()
    {
        return new Enumerator(_itemOrArray, _count);
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        return GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    static void ThrowIndexOutOfRange(int index)
    {
        throw new ArgumentOutOfRangeException(nameof(index), index, "Index out of range");
    }

    static void ThrowEmptyList()
    {
        throw new InvalidOperationException("Cannot pop empty List");
    }

    public struct Enumerator : IEnumerator<T>
    {
        readonly object? _itemOrArray;
        readonly uint _count;
        int _position;

        public Enumerator(object? itemOrArray, uint count)
        {
            _itemOrArray = itemOrArray;
            _count = count;
            _position = -1;
        }

        public bool MoveNext()
        {
            _position++;
            return (uint)_position < _count;
        }

        public void Reset()
        {
            _position = -1;
        }

        public readonly T Current => _count == 1 ? (T)_itemOrArray! : ((T[])_itemOrArray!)[_position];

        object IEnumerator.Current => Current;

        public void Dispose()
        {
        }
    }
}
