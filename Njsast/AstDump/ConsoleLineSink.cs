using System;

namespace Njsast.AstDump
{
    public class ConsoleLineSink : ILineSink
    {
        public void Print(string line)
        {
            Console.WriteLine(line);
        }
    }
}