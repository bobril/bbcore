using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Lib.Utils
{
    static public class ResourceUtils
    {
        static public string GetText(string name)
        {
            using (var stream = typeof(ResourceUtils).Assembly.GetManifestResourceStream(name))
            {
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        static public IDictionary<string, byte[]> GetZip(string name)
        {
            var result = new Dictionary<string, byte[]>();
            using (var stream = typeof(ResourceUtils).Assembly.GetManifestResourceStream(name))
            {
                using (var zip = new ZipArchive(stream!, ZipArchiveMode.Read))
                {
                    foreach (var entry in zip.Entries)
                    {
                        using var onestream = entry.Open();
                        var buf = new byte[entry.Length];
                        var ms = new MemoryStream(buf);
                        onestream.CopyTo(ms);
                        result["/" + entry.FullName] = buf;
                    }
                }
            }

            return result;
        }
    }
}
