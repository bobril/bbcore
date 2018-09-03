using System;

namespace Lib.Utils.Logger
{
    public interface IConsoleLogger : ILogger
    {
        void WriteLine(string message, ConsoleColor color);
        void SetColor(ConsoleColor color);
        void ClearColor();
    }
    
    public class ConsoleLogger : IConsoleLogger
    {
        ConsoleColor _previousColor = Console.ForegroundColor;

        public void WriteLine(string message)
        {
            Console.WriteLine(message);
        }

        public void WriteLine(string message, ConsoleColor color)
        {
            SetColor(color);
            Console.WriteLine(message);
            ClearColor();
        }

        public void Success(string message)
        {
            SetColor(ConsoleColor.Green);
            Console.WriteLine(message);
            ClearColor();
        }

        public void Info(string message)
        {
            Console.WriteLine(message);
        }

        public void Warn(string message)
        {
            SetColor(ConsoleColor.Yellow);
            Console.WriteLine(message);
            ClearColor();
        }

        public void Error(string message)
        {
            SetColor(ConsoleColor.Red);
            Console.WriteLine(message);
            ClearColor();
        }

        public void SetColor(ConsoleColor color)
        {
            _previousColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
        }

        public void ClearColor()
        {
            Console.ForegroundColor = _previousColor;
        }
    }
}