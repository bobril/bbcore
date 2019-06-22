using System.Collections.Generic;
using BTDB.ODBLayer;
using Lib.TSCompiler;
using Njsast.Bobril;
using Njsast.SourceMap;

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

        public string Output { get; set; }
        public SourceMap MapLink { get; set; }
        public SourceInfo SourceInfo { get; set; }
        public List<DependencyTriplet> TranspilationDependencies { get; set; } 
    }

    public interface ITSFileBuildCacheTable
    {
        bool Upsert(TSFileBuildCache value);
        TSFileBuildCache FindByIdOrDefault(byte[] contentHash, uint configurationId);
    }
}
