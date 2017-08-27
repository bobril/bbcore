using System;
using System.Collections.Generic;

namespace Lib.Utils
{
    public class SourceMap
    {
        public SourceMap()
        {
            version = 3;
        }

        public int version { get; set; }
        public string file { get; set; }
        public string sourceRoot { get; set; }
        public List<string> sources { get; set; }
        public List<string> sourcesContent { get; set; }
        public List<string> names { get; set; }
        public string mappings { get; set; }

        public override string ToString()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(this);
        }

        public static SourceMap Empty()
        {
            return new SourceMap
            {
                sources = new List<string>(),
                mappings = ""
            };
        }

        public static SourceMap Parse(string content)
        {
            var res = Newtonsoft.Json.JsonConvert.DeserializeObject<SourceMap>(content);
            if (res.version != 3) throw new Exception("Invalid Source Map version " + res.version);
            return res;
        }

        public static string RemoveLinkToSourceMap(string content)
        {
            var pos = content.Length - 3;
            while (pos >= 0)
            {
                if (content[pos] == 10) break;
                pos--;
            }
            if (pos < content.Length - 5)
            {
                if (content.Substring(pos + 1, 3) == "//#")
                    return content.Substring(0, pos);
            }
            return content;
        }
    }
}
