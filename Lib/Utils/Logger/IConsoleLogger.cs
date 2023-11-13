using System;

namespace Lib.Utils.Logger;

public interface IConsoleLogger : ILogger
{
    void WriteLine(string message, ConsoleColor color);
}