using System;
using System.Threading.Tasks;

namespace Lib.SCSSProcessor;

public interface IScssProcessor: IDisposable
{
    Task<string> ProcessScss(string source, string from, Func<string, string> canonicalize, Func<string, string> loader, Action<string> log);
}
