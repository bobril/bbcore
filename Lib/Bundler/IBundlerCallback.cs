using System;
using System.Collections.Generic;
using System.Text;

namespace Lib.Bundler
{
    public interface IBundlerCallback
    {
        string ReadContent(string name);
        void WriteBundle(string name, string content);
        /// <summary>
        /// Empty forName means main bundle.js
        /// </summary>
        string GenerateBundleName(string forName);
        string ResolveRequire(string name, string from);
        string TslibSource(bool withImport);
    }
}
