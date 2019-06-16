using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Njsast.Utils
{
    static public class PathUtils
    {
        public static string Normalize(string path)
        {
            if (path.Length >= 2 && path[1] == ':' && char.IsLower(path[0]))
            {
                path = char.ToUpperInvariant(path[0]) + path.Substring(1);
            }
            path = path.Replace('\\', '/').Replace("/./", "/");
            int idx;
            while ((idx = path.IndexOf("/../", StringComparison.Ordinal)) > 0)
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

        public static string Parent(string path)
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
                    return path.Substring(0, p);
                }
                if (path[p] == '/')
                    p--;
                while (path[p] != '/')
                    p--;
                if (p == 0)
                    return "/";
                return path.Substring(0, p);
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
                return path.Substring(0, 3);
            }
            return path.Substring(0, p);
        }

        public static string Subtract(string pathA, string pathB)
        {
            if (pathB.EndsWith("/")) pathB = pathB.Substring(0, pathB.Length - 1);
            if (pathA.Length > pathB.Length + 1 && pathA.StartsWith(pathB) && pathA[pathB.Length] == '/')
            {
                return pathA.Substring(pathB.Length + 1);
            }
            var commonStart = 0;
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

        public static (string, string) SplitDirAndFile(string path)
        {
            var dir = Parent(path);
            if (dir == null)
                return (null, path);
            return (dir, path.Substring(dir.Length + 1));
        }

        public static string Join(string dir1, string dir2)
        {
            if (Path.IsPathRooted(dir2))
                return dir2;
            if (dir1 == "/") dir1 = "";
            return Normalize(dir1 + "/" + dir2);
        }

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

        public static string ChangeExtension(string fileName, string newExtension)
        {
            var slashPos = fileName.LastIndexOf('/');
            var dotPos = fileName.LastIndexOf('.');
            if (dotPos <= slashPos + 1)
            {
                return $"{fileName}.{newExtension}";
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

        public static IEnumerable<(string name, bool isDir)> EnumParts(string path)
        {
            var pos = 0;
            var len = path.Length;
            if (pos < len && path[pos] == '/')
                pos++;
            if (pos < len && path[pos] == '/')
            {
                int pos2 = path.IndexOf('/', 2);
                if (pos2 < 0)
                {
                    yield return (path, true);
                    yield break;
                }
                yield return (path.Substring(0, pos2), true);
                pos = pos2 + 1;
            }
            while (pos < len)
            {
                int pos2 = path.IndexOf('/', pos + 1);
                if (pos2 < 0)
                {
                    yield return (path.Substring(pos), false);
                    yield break;
                }
                yield return (path.Substring(pos, pos2 - pos), true);
                pos = pos2 + 1;
            }
        }

        public static string GetExtension(string path)
        {
            var lastDotIndex = path.LastIndexOf('.');
            if (lastDotIndex < 0)
                return "";
            return path.Substring(lastDotIndex + 1);
        }

        public static string CommonDir(string p1, string p2)
        {
            var len = Math.Min(p1.Length, p2.Length);
            var pos = 0;

            while (pos < len)
            {
                var pos1 = p1.IndexOf('/', pos + 1);
                var pos2 = p2.IndexOf('/', pos + 1);
                if (pos1 < 0) pos1 = p1.Length;
                if (pos2 < 0) pos2 = p2.Length;
                if (pos1 != pos2 || p1.Substring(0, pos1) != p2.Substring(0, pos2))
                    return p1.Substring(0, pos);
                pos = pos1;
            }
            return p1.Substring(0, pos);
        }
    }
}
