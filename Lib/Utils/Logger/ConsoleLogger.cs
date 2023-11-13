using System;

namespace Lib.Utils.Logger;

public class ConsoleLogger : IConsoleLogger
{
    readonly object _lock = new();
    public bool Verbose { get; set; }

    public void WriteLine(string message)
    {
        lock (_lock)
        {
            Console.WriteLine(message);
        }
    }

    public void WriteLine(string message, ConsoleColor color)
    {
        lock (_lock)
        {
            SetColor(color);
            Console.WriteLine(message);
            ClearColor();
        }
    }

    public void Success(string message)
    {
        lock (_lock)
        {
            SetColor(ConsoleColor.Green);
            Console.WriteLine(message);
            ClearColor();
        }
    }

    public void Info(string message)
    {
        lock (_lock)
        {
            Console.WriteLine(message);
        }
    }

    public void Warn(string message)
    {
        lock (_lock)
        {
            SetColor(ConsoleColor.Yellow);
            Console.WriteLine(message);
            ClearColor();
        }
    }

    public void Error(string message)
    {
        lock (_lock)
        {
            SetColor(ConsoleColor.Red);
            Console.WriteLine(message);
            ClearColor();
        }
    }

    static void SetColor(ConsoleColor color)
    {
        Console.ForegroundColor = color;
    }

    static void ClearColor()
    {
        Console.ResetColor();
    }
}