using System;
using System.Threading.Tasks;
using Njsast.Css;

namespace Lib.SCSSProcessor;

public interface IScssProcessor: IDisposable
{
    Task<string> ProcessScss(string source, string from, Func<string, string> canonicalize, Func<string, string> loader, Action<string> log);
    Task<CssStylesheet> ProcessScssToCssAst(string source, string from, Func<string, string> canonicalize,
        Func<string, string> loader, Action<string> log);
}
