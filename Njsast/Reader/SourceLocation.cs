using System;

namespace Njsast.Reader
{
    public struct SourceLocation : IEquatable<SourceLocation>
    {
        public SourceLocation(Position start, Position end, string? sourceFile = null)
        {
            Start = start;
            End = end;
            Source = sourceFile;
        }

        public Position Start { get; }
        public Position End { get; }
        public string? Source { get; }

        public override string ToString()
        {
            if (Source == null)
                return $"(Start: {Start}, End: {End})";
            return $"(Start: {Start}, End: {End}, Source: {Source})";
        }

        public bool Equals(SourceLocation other)
        {
            return Equals(Start, other.Start) && Equals(End, other.End);
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is SourceLocation location && Equals(location);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Start.GetHashCode();
                hashCode = (hashCode * 397) ^ End.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(SourceLocation left, SourceLocation right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(SourceLocation left, SourceLocation right)
        {
            return !Equals(left, right);
        }
    }
}
