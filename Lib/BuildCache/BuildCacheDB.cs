using System.Collections.Generic;
using BTDB.FieldHandler;
using BTDB.ODBLayer;
using Lib.TSCompiler;
using Njsast.Bobril;
using Njsast.SourceMap;

namespace Lib.BuildCache;

public class TSConfiguration
{
    [PrimaryKey(1)]
    public string Version { get; set; }
    [PrimaryKey(2)]
    public string CompilerOptionsJson { get; set; }

    public uint Id { get; set; }
}

[PersistedName("tsconf")]
public interface ITSConfigurationTable: IRelation<TSConfiguration>
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

[PersistedName("ts4")]
public interface ITSFileBuildCacheTable: IRelation<TSFileBuildCache>
{
    TSFileBuildCache FindByIdOrDefault(byte[] contentHash, uint configurationId);
}