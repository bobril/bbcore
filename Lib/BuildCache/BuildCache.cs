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
        void StartTransaction();
        void EndTransaction();

        uint MapConfiguration(string tsversion, string compilerOptionsJson);
        void Store(TSFileBuildCache value);
        TSFileBuildCache FindTSFileBuildCache(byte[] contentHash, uint configurationId);
    }

    public class DummyBuildCache : IBuildCache
    {
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
    }

    public class TSConfiguration
    {
        [PrimaryKey(1)]
        public string Version { get; set; }
        [PrimaryKey(2)]
        public string CompilerOptionsJson { get; set; }

        public uint Id { get; set; }
    }

    public interface ITSConfiguration: IReadOnlyCollection<TSConfiguration>
    {
        void Insert(TSConfiguration value);
        TSConfiguration FindById(string version, string compilerOptionsJson);
    }

    public class TSFileBuildCache
    {
        [PrimaryKey(1)]
        public byte[] ContentHash { get; set; }
        [PrimaryKey(2)]
        public uint ConfigurationId { get; set; }

        public string DtsOutput { get; set; }
        public string JsOutput { get; set; }
        public SourceMap MapLink { get; set; }
        public List<string> ModuleImports { get; set; }
        public List<byte[]> ModuleImportsHashes { get; set; }
        public List<string> LocalImports { get; set; }
        public List<byte[]> LocalImportsHashes { get; set; }
    }

    public interface ITSFileBuildCache
    {
        void Insert(TSFileBuildCache value);
        TSFileBuildCache FindById(byte[] contentHash, uint configurationId);
    }

    public class PersistentBuildCache : IBuildCache
    {
        string _dir;
        IFileCollection _diskFileCollection;
        IKeyValueDB _kvdb;
        IObjectDB _odb;
        Func<IObjectDBTransaction, ITSConfiguration> _tsConfiguration;
        Func<IObjectDBTransaction, ITSFileBuildCache> _tsRelation;
        IObjectDBTransaction _tr;

        public PersistentBuildCache(string dir)
        {
            _dir = dir + "/cache";
            if (!new DirectoryInfo(_dir).Exists)
                Directory.CreateDirectory(_dir);

            _diskFileCollection = new OnDiskFileCollection(_dir);
            _kvdb = new KeyValueDB(new KeyValueDBOptions {
                FileCollection = _diskFileCollection,
                Compression = new SnappyCompressionStrategy(),
                FileSplitSize = 100000000
            });
            _odb = new ObjectDB();
            _odb.Open(_kvdb, false);
            using (var tr = _odb.StartWritingTransaction().Result)
            {
                _tsConfiguration = tr.InitRelation<ITSConfiguration>("tsconf");
                _tsRelation = tr.InitRelation<ITSFileBuildCache>("ts");
                tr.Commit();
            }
        }

        public void Dispose()
        {
            _odb.Dispose();
            _kvdb.Dispose();
            _diskFileCollection.Dispose();
        }

        public void EndTransaction()
        {
            _tr.Commit();
            _tr.Dispose();
            _tr = null;
        }

        public TSFileBuildCache FindTSFileBuildCache(byte[] contentHash, uint configurationId)
        {

            throw new NotImplementedException();
        }

        public uint MapConfiguration(string tsversion, string compilerOptionsJson)
        {
            var configRelation = _tsConfiguration(_tr);
            var cfg = configRelation.FindById(tsversion, compilerOptionsJson);
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
