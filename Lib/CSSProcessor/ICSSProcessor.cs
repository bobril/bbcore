using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Lib.CSSProcessor
{
    public interface ICssProcessor
    {
        Task<string> ProcessCss(string source, string from, Func<string, string, string> urlReplacer);
        Task<string> ConcatenateAndMinifyCss(IEnumerable<SourceFromPair> inputs, Func<string, string, string> urlReplacer);
    }
}
