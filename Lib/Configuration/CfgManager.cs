using System;
using System.IO;
using System.Text.Json;

namespace Lib.Configuration;

public class CfgManager<T> where T : class, new()
{
    readonly string _configName;
    readonly bool _createIfNotFound;

    public CfgManager(string configName, bool createIfNotFound)
    {
        _configName = configName;
        _createIfNotFound = createIfNotFound;
    }

    public void Load()
    {
        var cfg = new T();
        try
        {
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DictionaryKeyPolicy = JsonNamingPolicy.CamelCase, ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true, IgnoreNullValues = false
            };
            if (File.Exists(_configName))
            {
                cfg = JsonSerializer.Deserialize<T>(File.ReadAllBytes(_configName), jsonOptions);
            }
            else if (_createIfNotFound)
            {
                File.WriteAllBytes(_configName, JsonSerializer.SerializeToUtf8Bytes(cfg, jsonOptions));
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Failed load configuration " + e);
        }

        Cfg = cfg;
    }

    public T Cfg { get; private set; }
}