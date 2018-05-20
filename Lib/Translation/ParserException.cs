using System;

namespace Lib.Translation
{
    public class ParserException: Exception
    {
        public ParserException(string message, int position, int line, int column): base(message)
        {
            Position = position;
            Line = line;
            Column = column;
        }

        public int Position { get; }

        public int Line { get; }

        public int Column { get; }
    }
}