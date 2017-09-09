using System.Collections.Generic;

namespace Lib.TSCompiler
{
    public class SourceInfo
    {
        public struct Asset
        {
            public long nodeId;
            public string name;
        }
        public List<Asset> assets { get; set; }

        public struct Sprite
        {
            public long nodeId;
            public string name;
            public string color;
            public int? width;
            public int? height;
            public int? x;
            public int? y;
        }
        public List<Sprite> sprites { get; set; }

        public struct Translation
        {
            public long nodeId;
            public string message;
            public string hint;
            public bool justFormat;
            public bool withParams;
            public List<string> knownParams;
        }
        public List<Translation> translations;

        public struct StyleDef
        {
            public long nodeId;
            public string name;
            public bool userNamed;
            public bool isEx;
        }
        public List<StyleDef> styleDefs;
    }
}
