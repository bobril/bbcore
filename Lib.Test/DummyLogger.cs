using Lib.Utils.Logger;

namespace Lib.Test
{
    public class DummyLogger : ILogger
    {
        public bool Verbose { get; set; }

        public void WriteLine(string message)
        {
        }

        public void Success(string message)
        {
        }

        public void Info(string message)
        {
        }

        public void Warn(string message)
        {
        }

        public void Error(string message)
        {
        }
    }
}