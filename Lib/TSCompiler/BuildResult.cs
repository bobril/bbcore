using System;
using System.Collections.Generic;
using System.Diagnostics;
using Lib.Utils;
using Njsast;
using Njsast.SourceMap;

namespace Lib.TSCompiler
{
    public class ResolveResult
    {
        public string FileName;
        public TSProject Module;
        public StructList<string> NegativeChecks;
        public int IterationId;
        public string FileNameJs;

        internal string FileNameWithPreference(bool preferDts)
        {
            if (preferDts) return FileName;
            return FileNameJs ?? FileName;
        }
    }

    public class BuildResult
    {
        public BuildResult(ProjectOptions options)
        {
            CompressFileNames = options.CompressFileNames;
            OutputSubDir = options.OutputSubDir;
            BundleJsUrl = AllocateName("bundle.js");
        }

        public Dictionary<(string From, string Name), ResolveResult> ResolveCache = new Dictionary<(string From, string Name), ResolveResult>();
        public Dictionary<string, TSFileAdditionalInfo> Path2FileInfo = new Dictionary<string, TSFileAdditionalInfo>();
        public HashSet<TSFileAdditionalInfo> RecompiledIncrementaly = new HashSet<TSFileAdditionalInfo>();
        public StructList<TSFileAdditionalInfo> JavaScriptAssets;
        public Dictionary<string, TSProject> Modules = new Dictionary<string, TSProject>();
        public string CommonSourceDirectory;
        public Dictionary<string, int> Extension2LastNameIdx = new Dictionary<string, int>();
        public HashSet<string> TakenNames = new HashSet<string>();
        public bool HasError;
        public bool Incremental;
        public readonly bool CompressFileNames;
        public readonly string OutputSubDir;
        public readonly string BundleJsUrl;

        public string ToOutputUrl(string fileName)
        {
            Path2FileInfo.TryGetValue(fileName, out var info);
            if (info == null)
            {
                Debug.Assert(false);
                return fileName;
            }

            if (info.OutputUrl == null)
                info.OutputUrl = AllocateName(PathUtils.Subtract(fileName, CommonSourceDirectory));
            return info.OutputUrl;
        }

        public string AllocateName(string niceName)
        {
            if (CompressFileNames)
            {
                string extension = PathUtils.GetExtension(niceName).ToString();
                if (extension != "")
                    extension = "." + extension;
                int idx = 0;
                Extension2LastNameIdx.TryGetValue(extension, out idx);
                do
                {
                    niceName = ToShortName(idx) + extension;
                    idx++;
                    if (OutputSubDir != null)
                        niceName = $"{OutputSubDir}/{niceName}";
                } while (TakenNames.Contains(niceName));

                Extension2LastNameIdx[extension] = idx;
            }
            else
            {
                if (OutputSubDir != null)
                    niceName = OutputSubDir + "/" + niceName;
                if (TakenNames.Contains(niceName))
                {
                    int counter = 0;
                    string extension = PathUtils.GetExtension(niceName).ToString();
                    if (extension != "")
                        extension = "." + extension;
                    string prefix = niceName.Substring(0, niceName.Length - extension.Length);
                    while (TakenNames.Contains(niceName))
                    {
                        counter++;
                        niceName = prefix + counter.ToString() + extension;
                    }
                }
            }

            TakenNames.Add(niceName);
            return niceName;
        }

        static string ToShortName(int idx)
        {
            Span<char> res = stackalloc char[8];
            var resLen = 0;
            do
            {
                res[resLen++] = (char)(97 + idx % 26);
                idx = idx / 26 - 1;
            } while (idx >= 0);

            return new string(res.Slice(0, resLen));
        }

        internal string ToOutputUrl(TSFileAdditionalInfo source)
        {
            if (source.OutputUrl == null)
            {
                return ToOutputUrl(source.Owner.FullPath);
            }
            return source.OutputUrl;
        }
    }
}
