using System;
using System.Runtime.CompilerServices;
using Njsast.Reader;

namespace Njsast.Utils;

public struct LineCol : IEquatable<LineCol>, IComparable<LineCol>, IComparable
{
    public LineCol(int line, int col)
    {
        Line = line;
        Col = col;
    }

    public LineCol(Position pos)
    {
        Line = pos.Line;
        Col = pos.Column;
    }

    public readonly int Line;
    public readonly int Col;

    public override string ToString()
    {
        return Line + ":" + Col;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    ulong Key() => ((ulong) Line << 32) + (ulong) Col;

    public bool Equals(LineCol other)
    {
        return Line == other.Line && Col == other.Col;
    }

    public override bool Equals(object? obj)
    {
        return obj is LineCol other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Line, Col);
    }

    public static bool operator ==(LineCol left, LineCol right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(LineCol left, LineCol right)
    {
        return !left.Equals(right);
    }

    public static bool operator <(LineCol left, LineCol right)
    {
        return left.Key() < right.Key();
    }

    public static bool operator >(LineCol left, LineCol right)
    {
        return left.Key() > right.Key();
    }

    public static bool operator <=(LineCol left, LineCol right)
    {
        return left.Key() <= right.Key();
    }

    public static bool operator >=(LineCol left, LineCol right)
    {
        return left.Key() >= right.Key();
    }

    public int CompareTo(LineCol other)
    {
        return Key().CompareTo(other.Key());
    }

    public int CompareTo(object? obj)
    {
        if (ReferenceEquals(null, obj)) return 1;
        return obj is LineCol other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(LineCol)}");
    }
}