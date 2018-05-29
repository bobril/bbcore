using System;
using System.Collections.Generic;

namespace Lib.Configuration
{
    public interface IConfiguration
    {
        IConfigurationDescription Description { get; }

        IConfiguration Parent { get; }
        IReadOnlyList<CfgDesc> Describe();
        CfgDesc Describe(string key);

        IDictionary<string, object> Storage { get; }

        string Get(string key, string @default);
        int Get(string key, int @default);
        bool Get(string key, bool @default);
        T Get<T>(string key, T @default) where T : Enum;
        bool Has(string key);

        void Set(string key, string value);
        void Set(string key, int value);
        void Set(string key, bool value);
        void Set<T>(string key, T value) where T : Enum;
    }
}
