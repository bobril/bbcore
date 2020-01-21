using System;
using System.Collections.Generic;
using BTDB.Collections;
using Lib.Utils;

namespace Lib.TSCompiler
{
    public class MainBuildResult
    {
        public readonly bool CompressFileNames;
        public readonly string OutputSubDir;
        public bool PreserveProjectRoot;

        public readonly string OutputSubDirPrefix;

        // value could be string or byte[] or Lazy<string|byte[]>
        public readonly RefDictionary<string, object> FilesContent = new RefDictionary<string, object>();
        public readonly HashSet<string> TakenNames = new HashSet<string>();
        readonly Dictionary<string, int> Extension2LastNameIdx = new Dictionary<string, int>();

        public string? CommonSourceDirectory;
        public string? ProxyUrl;

        public MainBuildResult(bool compressFileNames, string? outputSubDir)
        {
            CompressFileNames = compressFileNames;
            OutputSubDir = outputSubDir;
            OutputSubDirPrefix = outputSubDir == null ? "" : outputSubDir + "/";
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
    }
}
