using System.Collections.Generic;

namespace Lib.TSCompiler
{
    public struct SpriteSlice
    {
        public string name;
        public float quality;
        public int width;
        public int height;
    }

    public class SourceInfo
    {
        public struct Asset
        {
            public long nodeId;
            public string name;
        }
        public List<Asset> assets;

        public struct Sprite
        {
            public long nodeId;
            public string name;
            public string color;
            public bool? hasColor;
            public int? width;
            public int? height;
            public int? x;
            public int? y;
            public int owidth;
            public int oheight;
            public int ox;
            public int oy;
            public SpriteSlice[] slices;
        }

        public List<Sprite> sprites;

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

        public bool IsEmpty
        {
            get
            {
                if (assets?.Count > 0)
                    return false;
                if (sprites?.Count > 0)
                    return false;
                if (styleDefs?.Count > 0)
                    return false;
                if (translations?.Count > 0)
                    return false;
                return true;
            }
        }
    }
}
