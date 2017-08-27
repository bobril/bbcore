using System.IO;
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
    }
}
