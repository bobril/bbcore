using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BTDB.Collections;
using Lib.Utils;

namespace Lib.TSCompiler
{
    public class MainBuildResult
    {
        public readonly bool CompressFileNames;
        public readonly string? OutputSubDir;
        public readonly string? SpriteOutputPathOverride;
        public bool PreserveProjectRoot;

        public readonly string OutputSubDirPrefix;

        // value could be string or byte[] or Lazy<string|byte[]>
        public readonly RefDictionary<string, object> FilesContent = new();
        public readonly HashSet<string> TakenNames = new();
        readonly Dictionary<string, int> Extension2LastNameIdx = new();

        public string? CommonSourceDirectory;
        public string? ProxyUrl;

        public MainBuildResult(bool compressFileNames, string? outputSubDir, string? spriteOutputPathOverride)
        {
            CompressFileNames = compressFileNames;
            OutputSubDir = outputSubDir;
            OutputSubDirPrefix = outputSubDir == null ? "" : outputSubDir + "/";
            SpriteOutputPathOverride = spriteOutputPathOverride;
        }

        public string AllocateName(string niceName, bool allowCompressAndPlacingInSubDir = true)
        {
            if (CompressFileNames && allowCompressAndPlacingInSubDir)
            {
                var extension = PathUtils.GetExtension(niceName).ToString();
                if (extension != "")
                    extension = "." + extension;
                Extension2LastNameIdx.TryGetValue(extension, out var idx);
                do
                {
                    niceName = ToShortName(idx) + extension;
                    idx++;
                    if (allowCompressAndPlacingInSubDir && OutputSubDir != null)
                        niceName = OutputSubDirPrefix + niceName;
                } while (TakenNames.Contains(niceName));

                Extension2LastNameIdx[extension] = idx;
            }
            else
            {
                if (allowCompressAndPlacingInSubDir && OutputSubDir != null)
                    niceName = PathUtils.Normalize(OutputSubDirPrefix + niceName);
                if (TakenNames.Contains(niceName))
                {
                    var counter = 0;
                    var extension = PathUtils.GetExtension(niceName).ToString();
                    if (extension != "")
                        extension = "." + extension;
                    var prefix = niceName.Substring(0, niceName.Length - extension.Length);
                    while (TakenNames.Contains(niceName))
                    {
                        counter++;
                        niceName = prefix + counter + extension;
                    }
                }
            }

            TakenNames.Add(niceName);
            return niceName;
        }

        public static string ToShortName(long idx)
        {
            Span<char> res = stackalloc char[14];
            var resLen = 0;
            do
            {
                res[resLen++] = (char) (97 + idx % 26);
                idx = idx / 26 - 1;
            } while (idx >= 0);

            return new string(res.Slice(0, resLen));
        }

        public void MergeCommonSourceDirectory(string path)
        {
            if (PreserveProjectRoot)
            {
                CommonSourceDirectory ??= path;
            }
            else
            {
                CommonSourceDirectory =
                    CommonSourceDirectory == null ? path : PathUtils.CommonDir(CommonSourceDirectory, path);
            }
        }

        string ApplySpritePathOverride(string bundlePng)
        {
            if (SpriteOutputPathOverride == null)
                return bundlePng;
            if (OutputSubDir != null)
            {
                bundlePng = PathUtils.Subtract(bundlePng, OutputSubDir);
            }

            return PathUtils.Join(SpriteOutputPathOverride, bundlePng);
        }

        public string GenerateCodeForBobrilBPath(string? bundlePng, List<float>? bundlePngInfo)
        {
            if (bundlePng == null) return "";
            var res = new StringBuilder();
            var spritePath = ApplySpritePathOverride(bundlePng);
            res.AppendFormat("var bobrilBPath=\"{0}\"", spritePath);
            if (bundlePngInfo!.Count > 1)
            {
                res.Append(",bobrilBPath2=[");
                for (var i = 1; i < bundlePngInfo!.Count; i++)
                {
                    var q = bundlePngInfo![i];
                    if (i > 1) res.Append(",");
                    res.AppendFormat("[\"{0}\",{1}]", PathUtils.InjectQuality(spritePath, q), q.ToString(CultureInfo.InvariantCulture));
                }

                res.Append("]");
            }

            res.Append(";");

            return res.ToString();
        }
    }
}
