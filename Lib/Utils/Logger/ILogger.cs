using System;
using System.Runtime.InteropServices;

namespace Lib.Utils.Logger
{
    public enum LoggerSeverity
    {
        Info,
        Warn,
        Error
    }
    
    public interface ILogger
    {
        void WriteLine(string message);
        void Success(string message);
        void Info(string message);
        void Warn(string message);
        void Error(string message);
        bool Verbose { get; set; }
    }
}