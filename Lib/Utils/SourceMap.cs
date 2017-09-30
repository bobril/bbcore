using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

        public static SourceMap Identity(string content, string fileName)
        {
            var sb = new StringBuilder();
            sb.Append("AAAA");
            for (var i = 0; i < content.Length; i++)
                if (content[i] == '\n') sb.Append(";AACA");
            return new SourceMap
            {
                sources = new List<string> { fileName },
                mappings = sb.ToString()
            };
        }

        public static SourceMap Parse(string content, string dir)
        {
            var res = Newtonsoft.Json.JsonConvert.DeserializeObject<SourceMap>(content);
            if (res.version != 3) throw new Exception("Invalid Source Map version " + res.version);
            if (dir!=null)
            {
                res.sources = res.sources.Select(s => PathUtils.Join(dir, s)).ToList();
            }
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
