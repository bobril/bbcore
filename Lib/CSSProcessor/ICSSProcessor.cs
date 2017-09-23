using System;
using System.Threading.Tasks;

namespace Lib.CSSProcessor
{
    public interface ICssProcessor
    {
        Task<string> ProcessCss(string source, string from, Func<string, string, string> urlReplacer);
    }
}
