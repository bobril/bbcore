using System;

namespace Njsast.Reader
{
    public struct Position : IEquatable<Position>
    {
        public Position(int line, int column, int index)
        {
            Line = line;
            Column = column;
            Index = index;
        }

        public Position Increment(int i)
        {
            return new Position(Line, Column + i, Index + i);
        }

        public string ToShortString()
        {
            return $"{Line + 1}:{Column + 1}";
        }

        public override string ToString()
        {
            return $"(Line: {Line}, Column: {Column}, Index: {Index})";
        }

        public bool Equals(Position other)
        {
            return Line == other.Line && Column == other.Column && Index == other.Index;
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is Position position && Equals(position);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (((Line * 397) ^ Column) * 397) ^ Index;
            }
        }

        public readonly int Line;

        public readonly int Column;

        public readonly int Index;

        public static bool operator ==(Position left, Position right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Position left, Position right)
        {
            return !Equals(left, right);
        }

        public static int operator -(Position left, Position right)
        {
            return left.Index - right.Index;
        }
    }
}
