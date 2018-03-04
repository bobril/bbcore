using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BTDB.KVDBLayer;
using BTDB.ODBLayer;
using Lib.Utils;

namespace Lib.BuildCache
{
    public interface IBuildCache: IDisposable
    {
        bool IsEnabled { get; }
        void StartTransaction();
        void EndTransaction();

        uint MapConfiguration(string tsversion, string compilerOptionsJson);
        void Store(TSFileBuildCache value);
        TSFileBuildCache FindTSFileBuildCache(byte[] contentHash, uint configurationId);
    }
}
