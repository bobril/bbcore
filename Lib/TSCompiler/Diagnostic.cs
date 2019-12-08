using System;

namespace Lib.TSCompiler
{
    public class Diagnostic : IEquatable<Diagnostic>
    {
        public bool IsError;
        public bool IsSemantic;
        public int Code;
        public string Text;
        public string FileName;
        public int StartLine;
        public int StartCol;
        public int EndLine;
        public int EndCol;

        public override bool Equals(object? obj)
        {
            return Equals(obj as Diagnostic);
        }

        public bool Equals(Diagnostic other)
        {
            return other != null &&
                   IsError == other.IsError &&
                   Code == other.Code &&
                   Text == other.Text &&
                   FileName == other.FileName &&
                   StartLine == other.StartLine &&
                   StartCol == other.StartCol &&
                   EndLine == other.EndLine &&
                   EndCol == other.EndCol;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(IsError, Code, Text, FileName, StartLine, StartCol, EndLine, EndCol);
        }
    }
}
