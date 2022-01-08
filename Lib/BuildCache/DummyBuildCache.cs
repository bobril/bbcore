using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BTDB.KVDBLayer;
using BTDB.ODBLayer;
using Lib.Utils;

namespace Lib.BuildCache;

public class DummyBuildCache : IBuildCache
{
    public bool IsEnabled => false;

    public void Dispose()
    {
    }

    public void EndTransaction()
    {
    }

    public TSFileBuildCache FindTSFileBuildCache(byte[] contentHash, uint configurationId)
    {
        return null;
    }

    public string GetContentByHash(byte[] contentHash)
    {
        return null;
    }

    public uint MapConfiguration(string tsversion, string compilerOptionsJson)
    {
        return 0;
    }

    public void StartTransaction()
    {
    }

    public void Store(TSFileBuildCache value)
    {
    }

    public void Store(byte[] contentHash, string content)
    {
    }
}