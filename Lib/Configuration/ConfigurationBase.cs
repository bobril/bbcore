using System;
using System.Collections.Generic;

namespace Lib.Configuration
{
    public abstract class ConfigurationBase : IConfiguration
    {
        protected IConfigurationDescription _description;
        protected IConfiguration _parent;
        protected Dictionary<string, object> _storage;

        public IConfigurationDescription Description => _description;

        public IDictionary<string, object> Storage => _storage;

        public IConfiguration Parent => _parent;

        public IReadOnlyList<CfgDesc> Describe() => _description.Describe();

        public CfgDesc Describe(string key) => _description.Describe(key);

        protected object? HierarchicalGet(string key)
        {
            var that = (IConfiguration)this;
            while (that != null)
            {
                if (that.Storage.TryGetValue(key, out var result))
                    return result;
                that = that.Parent;
            }
            return null;
        }

        public string Get(string key, string @default)
        {
            var result = HierarchicalGet(key);
            if (result == null) return @default;
            if (result is string) return (string)result;
            throw new Exception(result.GetType().ToString() + " is not string");
        }

        public int Get(string key, int @default)
        {
            var result = HierarchicalGet(key);
            if (result == null) return @default;
            if (result is int) return (int)result;
            throw new Exception(result.GetType().ToString() + " is not int");
        }

        public bool Get(string key, bool @default)
        {
            var result = HierarchicalGet(key);
            if (result == null) return @default;
            if (result is bool) return (bool)result;
            throw new Exception(result.GetType().ToString() + " is not bool");
        }

        public bool Has(string key) => HierarchicalGet(key) != null;

        public void Set(string key, string value)
        {
            _storage[key] = value;
        }

        public void Set(string key, int value)
        {
            _storage[key] = value;
        }

        public void Set(string key, bool value)
        {
            _storage[key] = value;
        }

        T IConfiguration.Get<T>(string key, T @default)
        {
            var result = HierarchicalGet(key);
            if (result == null) return @default;
            if (result is T) return (T)result;
            throw new Exception(result.GetType().ToString() + " is not "+typeof(T).ToString());
        }

        void IConfiguration.Set<T>(string key, T value)
        {
            _storage[key] = value;
        }
    }
}
