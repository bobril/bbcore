using BTDB.StreamLayer;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Lib.Utils
{
    static public class PathUtils
    {
        public static readonly bool IsUnixFs;

        static PathUtils()
        {
            IsUnixFs = Path.DirectorySeparatorChar == '/';
        }

        public static string Normalize(string path)
        {
            if (path.Length >= 2 && path[1] == ':' && char.IsLower(path[0]))
            {
                path = char.ToUpperInvariant(path[0]) + path.Substring(1);
            }
            path = path.Replace('\\', '/').Replace("/./", "/");
            int idx;
            while ((idx = path.IndexOf("/../")) > 0)
            {
                int diridx = path.LastIndexOf('/', idx - 1);
                if (diridx >= 0)
                {
                    path = path.Remove(diridx, idx + 3 - diridx);
                }
                else
                {
                    path = path.Remove(0, idx + 4);
                }
            }
            return path;
        }

        public static ReadOnlySpan<char> Parent(ReadOnlySpan<char> path)
        {
            if (path.Length == 0)
                return null;
            var p = path.Length - 1;
            if (path[0] == '/')
            {
                if (p == 0)
                    return null;
                if (p > 1 && path[1] == '/')
                {
                    if (path[p] == '/')
                        p--;
                    while (path[p] != '/')
                        p--;
                    if (p == 1)
                        return null;
                    return path.Slice(0, p);
                }
                if (path[p] == '/')
                    p--;
                while (path[p] != '/')
                    p--;
                if (p == 0)
                    return "/";
                return path.Slice(0, p);
            }
            if (p <= 2)
                return null;
            if (path[p] == '/')
                p--;
            while (p >= 0 && path[p] != '/')
                p--;
            if (p < 0)
                return null;
            if (p == 2)
            {
                return path.Slice(0, 3);
            }
            return path.Slice(0, p);
        }

        public static string RealPath(string path)
        {
            var res = PlatformMethods.Instance.RealPath(path);
            if (res == null) return path;
            return Normalize(res);
        }

        static Regex _multiplierExtract = new Regex(@"@(\d+(?:\.\d+)?)\.(?:[^\.]+)$");

        public static (string Name, float Quality) ExtractQuality(string name)
        {
            var m = _multiplierExtract.Match(name);
            if (m.Success)
            {
                var capture = m.Groups[1];
                var mult = float.Parse(capture.Value);
                return (name.Substring(0, capture.Index - 1) + name.Substring(capture.Index + capture.Length), mult);
            }
            else
            {
                return (name, 1);
            }
        }

        public static string Subtract(string pathA, string pathB)
        {
            if (pathB.EndsWith("/")) pathB = pathB.Substring(0, pathB.Length - 1);
            if (pathA.Length > pathB.Length + 1 && pathA.StartsWith(pathB) && pathA[pathB.Length] == '/')
            {
                return pathA.Substring(pathB.Length + 1);
            }
            int commonStart = 0;
            while (true)
            {
                var slash = pathA.IndexOf('/', commonStart);
                if (slash < 0 || pathB.Length <= slash)
                    break;
                if (pathB.Substring(commonStart, slash - commonStart + 1) != pathA.Substring(commonStart, slash - commonStart + 1))
                {
                    break;
                }
                commonStart = slash + 1;
            }
            var upCount = pathB.Skip(commonStart).Count(ch => ch == '/');
            var sb = new StringBuilder();
            while (upCount >= 0)
            {
                sb.Append("../");
                upCount--;
            }
            sb.Append(pathA.Substring(commonStart));
            return sb.ToString();
        }

        internal static string GetFile(string fn)
        {
            SplitDirAndFile(fn, out var file);
            return file.ToString();
        }

        public static ReadOnlySpan<char> SplitDirAndFile(ReadOnlySpan<char> path, out ReadOnlySpan<char> file)
        {
            var dir = Parent(path);
            if (dir.Length == 0)
            {
                file = path;
                return null;
            }
            file = path.Slice(dir.Length + 1);
            return dir;
        }

        public static string Join(ReadOnlySpan<char> dir1, string dir2)
        {
            if (Path.IsPathRooted(dir2))
                return dir2;
            return Normalize(dir1.ToString() + "/" + dir2);
        }


        // Direct child
        public static bool IsChildOf(string child, string parent)
        {
            if (child.Length <= parent.Length + 1)
                return false;
            if (!child.StartsWith(parent, StringComparison.Ordinal))
                return false;
            if (child[parent.Length] != '/')
                return false;
            if (child.IndexOf('/', parent.Length + 1) >= 0)
                return false;
            return true;
        }

        public static bool IsAnyChildOf(string child, string parent)
        {
            if (child.Length <= parent.Length + 1)
                return false;
            if (!child.StartsWith(parent, StringComparison.Ordinal))
                return false;
            if (child[parent.Length] != '/')
                return false;
            return true;
        }

        public static string ChangeExtension(string fileName, string newExtension)
        {
            var slashPos = fileName.LastIndexOf('/');
            var dotPos = fileName.LastIndexOf('.');
            if (dotPos <= slashPos + 1)
            {
                return fileName + '.' + newExtension;
            }
            return fileName.Substring(0, dotPos + 1) + newExtension;
        }

        public static string WithoutExtension(string fileName)
        {
            var slashPos = fileName.LastIndexOf('/');
            var dotPos = fileName.LastIndexOf('.');
            if (dotPos <= slashPos + 1)
            {
                return fileName;
            }
            return fileName.Substring(0, dotPos);
        }

        public static bool EnumParts(ReadOnlySpan<char> path, ref int pos, out ReadOnlySpan<char> name, out bool isDir)
        {
            if (pos < 0)
            {
                name = null;
                isDir = false;
                return false;
            }
            var len = path.Length;
            if (pos == 0)
            {
                if (pos < len && path[pos] == '/')
                    pos++;
                if (pos < len && path[pos] == '/')
                {
                    int pos2 = path.Slice(2).IndexOf("/");
                    if (pos2 < 0)
                    {
                        name = path;
                        isDir = true;
                        pos = -1;
                        return true;
                    }
                    name = path.Slice(2, pos2);
                    isDir = true;
                    pos = pos2 + 3;
                    return true;
                }
            }
            if (pos < len)
            {
                int pos2 = path.Slice(pos + 1).IndexOf("/");
                if (pos2 < 0)
                {
                    name = path.Slice(pos);
                    isDir = false;
                    pos = -1;
                    return true;
                }
                name = path.Slice(pos, pos2 + 1);
                isDir = true;
                pos += pos2 + 2;
                return true;
            }
            name = null;
            isDir = false;
            return false;
        }

        public static ReadOnlySpan<char> GetExtension(ReadOnlySpan<char> path)
        {
            var lastDotIndex = path.LastIndexOf('.');
            if (lastDotIndex < 1)
                return ReadOnlySpan<char>.Empty;
            return path.Slice(lastDotIndex + 1);
        }

        public static string PathToMimeType(string path)
        {
            var lastDotIndex = path.LastIndexOf('.');
            if (lastDotIndex < 0)
                return "application/unknown";
            var extension = path.Substring(lastDotIndex + 1);
            return ExtensionToMimeType(extension);
        }

        internal static string InjectQuality(string fn, float quality)
        {
            if (quality == 1) return fn;
            var lastDotIndex = fn.LastIndexOf('.');
            return fn.Insert(lastDotIndex, "@" + quality.ToString(CultureInfo.InvariantCulture));
        }

        public static string ExtensionToMimeType(string extension)
        {
            switch (extension)
            {
                case "png":
                    return "image/png";
                case "jpg":
                case "jpeg":
                    return "image/jpeg";
                case "gif":
                    return "image/gif";
                case "svg":
                    return "image/svg+xml";
                case "css":
                    return "text/css";
                case "html":
                case "htm":
                    return "text/html";
                case "jsx":
                case "js":
                    return "application/javascript";
                case "tsx":
                case "ts":
                    return "text/plain";
                case "map":
                case "json":
                    return "application/json";
            }
            return "application/unknown";
        }

        public static string CommonDir(string p1, string p2)
        {
            var len = Math.Min(p1.Length, p2.Length);
            if (len == p1.Length && (len < p2.Length && p2[len] == '/' || len == p2.Length) && p1.AsSpan().SequenceEqual(p2.AsSpan(0, len)))
            {
                return p1;
            }
            var pos = 0;

            while (pos < len)
            {
                var pos1 = p1.IndexOf('/', pos + 1);
                var pos2 = p2.IndexOf('/', pos + 1);
                if (pos1 < 0) pos1 = p1.Length;
                if (pos2 < 0) pos2 = p2.Length;
                if (pos1 != pos2 || !p1.AsSpan(0, pos1).SequenceEqual(p2.AsSpan(0, pos2)))
                    return p1.Substring(0, pos);
                pos = pos1;
            }
            return p1.Substring(0, pos);
        }

        internal static string DirToCreateDirectory(ReadOnlySpan<char> dir)
        {
            if (dir.Length == 0) return ".";
            return dir.ToString();
        }

        public static string ForDiagnosticDisplay(string name, string relativeTo, string rootToStayInside)
        {
            if (name == null) return null;
            if (rootToStayInside == null) rootToStayInside = relativeTo;
            var real = PlatformMethods.Instance.RealPath(name);
            if (real != null)
            {
                real = Normalize(real);
                if (real != name)
                {
                    if (IsAnyChildOf(real, rootToStayInside))
                    {
                        name = real;
                    }
                }
            }
            return name;
        }
    }
}
