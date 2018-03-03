using System.Collections.Generic;
using BTDB.ODBLayer;
using Lib.Utils;

namespace Lib.BuildCache
{
    public class TSConfiguration
    {
        [PrimaryKey(1)]
        public string Version { get; set; }
        [PrimaryKey(2)]
        public string CompilerOptionsJson { get; set; }

        public uint Id { get; set; }
    }

    public interface ITSConfigurationTable: IReadOnlyCollection<TSConfiguration>
    {
        void Insert(TSConfiguration value);
        TSConfiguration FindByIdOrDefault(string version, string compilerOptionsJson);
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

    public interface ITSFileBuildCacheTable
    {
        void Insert(TSFileBuildCache value);
        TSFileBuildCache FindByIdOrDefault(byte[] contentHash, uint configurationId);
    }
}
