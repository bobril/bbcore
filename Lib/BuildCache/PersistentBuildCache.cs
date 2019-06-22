using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using BTDB.KVDBLayer;
using BTDB.ODBLayer;

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
            while (cacheIndex < 100)
            {
                _mutex = new Mutex(false, @"Global\bbcoreCache" + cacheIndex);
                if (_mutex.WaitOne(10))
                    break;
                _mutex.Dispose();
                _mutex = null;
                cacheIndex++;
            }
            if (_mutex == null)
                return;
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
                _tsRelation = tr.InitRelation<ITSFileBuildCacheTable>("ts2");
                tr.Commit();
            }
        }

        public bool IsEnabled => _mutex != null;

        public void Dispose()
        {
            if (_mutex != null)
            {
                _odb.Dispose();
                _kvdb.Dispose();
                _diskFileCollection.Dispose();
                _mutex.ReleaseMutex();
                _mutex.Dispose();
            }
        }

        public void EndTransaction()
        {
            if (!IsEnabled) return;
            _tr.Commit();
            _tr.Dispose();
            _tr = null;
        }

        public TSFileBuildCache FindTSFileBuildCache(byte[] contentHash, uint configurationId)
        {
            if (!IsEnabled) return null;
            return _tsRelation(_tr).FindByIdOrDefault(contentHash, configurationId);
        }

        public uint MapConfiguration(string tsversion, string compilerOptionsJson)
        {
            if (!IsEnabled) return 0;
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
            if (!IsEnabled) return;
            _tr = _odb.StartTransaction();
        }

        public void Store(TSFileBuildCache value)
        {
            if (!IsEnabled) return;
            var relation = _tsRelation(_tr);
            relation.Upsert(value);
        }

        static bool CompareArrays(List<byte[]> a, List<byte[]> b)
        {
            if (a != null && a.Count == 0) a = null;
            if (b != null && b.Count == 0) b = null;
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (!a[i].AsSpan().SequenceEqual(b[i].AsSpan()))
                    return false;
            }
            return true;
        }

        static bool CompareArrays(List<string> a, List<string> b)
        {
            if (a != null && a.Count == 0) a = null;
            if (b != null && b.Count == 0) b = null;
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i])
                    return false;
            }
            return true;
        }
    }
}
