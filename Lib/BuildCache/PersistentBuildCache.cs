using System.IO;
using System.Threading;
using BTDB.KVDBLayer;
using BTDB.ODBLayer;

namespace Lib.BuildCache
{
    public class PersistentBuildCache : IBuildCache
    {
        readonly IFileCollection _diskFileCollection = null!;
        readonly IKeyValueDB _kvdb = null!;
        readonly IObjectDB _odb = null!;
        IObjectDBTransaction? _tr;
        readonly Mutex? _mutex;

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
            dir = dir + "/cache" + (cacheIndex == 0 ? "" : cacheIndex.ToString());
            if (!new DirectoryInfo(dir).Exists)
                Directory.CreateDirectory(dir);

            _diskFileCollection = new OnDiskFileCollection(dir);
            _kvdb = new KeyValueDB(new KeyValueDBOptions
            {
                FileCollection = _diskFileCollection,
                Compression = new SnappyCompressionStrategy(),
                FileSplitSize = 100000000
            });
            _odb = new ObjectDB();
            _odb.Open(_kvdb, false);
            using var tr = _odb.StartWritingTransaction().Result;
            tr.GetRelation<ITSConfigurationTable>();
            tr.GetRelation<ITSFileBuildCacheTable>();
            tr.Commit();
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
            _tr!.Commit();
            _tr.Dispose();
            _tr = null;
        }

        public TSFileBuildCache? FindTSFileBuildCache(byte[] contentHash, uint configurationId)
        {
            if (!IsEnabled) return null;
            return _tr!.GetRelation<ITSFileBuildCacheTable>().FindByIdOrDefault(contentHash, configurationId);
        }

        public uint MapConfiguration(string tsversion, string compilerOptionsJson)
        {
            if (!IsEnabled) return 0;
            var configRelation = _tr!.GetRelation<ITSConfigurationTable>();
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
            var relation = _tr!.GetRelation<ITSFileBuildCacheTable>();
            relation.Upsert(value);
        }
    }
}
