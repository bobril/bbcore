using Lib.ToolsDir;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lib.Bundler
{
    public interface IBundler
    {
        IReadOnlyList<string>? MainFiles { get; set; }
        /// <summary>
        /// Default is true
        /// </summary>
        bool Compress { get; set; }
        /// <summary>
        /// Default is true
        /// </summary>
        bool Mangle { get; set; }
        /// <summary>
        /// Default is false
        /// </summary>
        bool Beautify { get; set; }
        IReadOnlyDictionary<string, object> Defines { get; set; }

        IBundlerCallback? Callbacks { get; set; }

        void Bundle();
    }
}
