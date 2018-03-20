using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using BTDB.KVDBLayer;
using BTDB.ODBLayer;
using Lib.Utils;

namespace Lib.BuildCache
{
    public class PersistentBuildCache : IBuildCache
    {
        string _dir;
        IFileCollection _diskFileCollection;
        IKeyValueDB _kvdb;
        IObjectDB _odb;
        Func<IObjectDBTransaction, ITSConfigurationTable> _tsConfiguration;
        Func<IObjectDBTransaction, ITSFileBuildCacheTable> _tsRelation;
        IObjectDBTransaction _tr;
        Mutex _mutex;

        public PersistentBuildCache(string dir)
        {
            var cacheIndex = 0;
            while (_mutex == null)
            {
                _mutex = new Mutex(false, @"Global\bbcoreCache" + cacheIndex);
                if (_mutex.WaitOne(10))
                    break;
                _mutex.Dispose();
                cacheIndex++;
            }
            _dir = dir + "/cache" + (cacheIndex == 0 ? "" : cacheIndex.ToString());
            if (!new DirectoryInfo(_dir).Exists)
                Directory.CreateDirectory(_dir);

            _diskFileCollection = new OnDiskFileCollection(_dir);
            _kvdb = new KeyValueDB(new KeyValueDBOptions
            {
                FileCollection = _diskFileCollection,
                Compression = new SnappyCompressionStrategy(),
                FileSplitSize = 100000000
            });
            _odb = new ObjectDB();
            _odb.Open(_kvdb, false);
            using (var tr = _odb.StartWritingTransaction().Result)
            {
                _tsConfiguration = tr.InitRelation<ITSConfigurationTable>("tsconf");
                _tsRelation = tr.InitRelation<ITSFileBuildCacheTable>("ts");
                tr.Commit();
            }
        }

        public bool IsEnabled => true;

        public void Dispose()
        {
            _odb.Dispose();
            _kvdb.Dispose();
            _diskFileCollection.Dispose();
            if (_mutex != null)
            {
                _mutex.ReleaseMutex();
                _mutex.Dispose();
            }
        }

        public void EndTransaction()
        {
            _tr.Commit();
            _tr.Dispose();
            _tr = null;
        }

        public TSFileBuildCache FindTSFileBuildCache(byte[] contentHash, uint configurationId)
        {
            return _tsRelation(_tr).FindByIdOrDefault(contentHash, configurationId);
        }

        public uint MapConfiguration(string tsversion, string compilerOptionsJson)
        {
            var configRelation = _tsConfiguration(_tr);
            var cfg = configRelation.FindByIdOrDefault(tsversion, compilerOptionsJson);
            if (cfg != null)
            {
                return cfg.Id;
            }
            var id = (uint)configRelation.Count;
            configRelation.Insert(new TSConfiguration
            {
                Version = tsversion,
                CompilerOptionsJson = compilerOptionsJson,
                Id = id
            });
            return id;
        }

        public void StartTransaction()
        {
            _tr = _odb.StartTransaction();
        }

        public void Store(TSFileBuildCache value)
        {
            _tsRelation(_tr).Insert(value);
        }
    }
}
