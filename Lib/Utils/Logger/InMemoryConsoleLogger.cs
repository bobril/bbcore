using System;
using System.Collections.Generic;

namespace Lib.Utils.Logger;

public class InMemoryConsoleLogger : IConsoleLogger
{
    private readonly IList<string> _logs;

    public InMemoryConsoleLogger(IList<string> logs)
    {
        _logs = logs;
    }


    public void WriteLine(string message)
    {
        _logs.Add(message);
    }

    public void Success(string message)
    {
        _logs.Add(message);
    }

    public void Info(string message)
    {
        _logs.Add(message);
    }

    public void Warn(string message)
    {
        _logs.Add(message);
    }

    public void Error(string message)
    {
        _logs.Add(message);
    }

    public bool Verbose { get; set; }
    public void WriteLine(string message, ConsoleColor color)
    {
        _logs.Add(message);
    }
}