using System;

namespace Lib.Configuration
{
    public class CfgDesc
    {
        public CfgDesc(string key, Type type, string description, object @default)
        {
            Key = key;
            Type = type;
            Description = description;
            Default = @default;
        }

        public readonly string Key;
        public readonly Type Type;
        public readonly string Description;
        public readonly object Default;
    }
}
