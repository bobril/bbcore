using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Lib.DiskCache;
using Lib.Utils;

namespace Lib.TSCompiler
{
    public class ProjectOptions
    {
        public ToolsDir.IToolsDir Tools { get; set; }
        public TSProject Owner { get; set; }
        public string TestSourcesRegExp { get; set; }
        public Dictionary<string, bool> Defines { get; set; }
        public string Title { get; set; }
        public string HtmlHead { get; set; }
        public StyleDefNamingStyle StyleDefNaming { get; set; }
        public string PrefixStyleNames { get; set; }
        public string Example { get; set; }
        public bool BobrilJsx { get; set; }
        public TSCompilerOptions CompilerOptions;
        public string AdditionalResourcesDirectory;
        public string CommonSourceDirectory;
        public bool SpriteGeneration;
        public SpriteHolder SpriteGenerator;
        public string BundlePngUrl;
        public string BundleJsUrl;

        public string HtmlHeadExpanded { get; set; }
        public string MainFile { get; set; }
        public string JasmineDts { get; set; }
        public List<string> TestSources { get; set; }
        public List<string> ExampleSources { get; set; }
        public string BobrilJsxDts { get; set; }
        public FastBundleBundler MainProjFastBundle { get; set; }
        public FastBundleBundler TestProjFastBundle { get; internal set; }
        public bool LiveReloadEnabled { get; internal set; }
        public string TypeScriptVersion { get; internal set; }

        public bool Localize;
        public string DefaultLanguage;
        public DepedencyUpdate DependencyUpdate;
        public string OutputSubDir;
        public bool CompressFileNames;

        public Translation.TranslationDb TranslationDb;

        // value could be string or byte[]
        public Dictionary<string, object> FilesContent;
        internal string NpmRegistry;

        public Dictionary<string, int> Extension2LastNameIdx = new Dictionary<string, int>();
        public HashSet<string> TakenNames = new HashSet<string>();

        public void RefreshMainFile()
        {
            MainFile = PathUtils.Join(Owner.Owner.FullPath, Owner.MainFile);
        }

        string ToShortName(int idx)
        {
            var res = "";
            do
            {
                res += (char)(97 + idx % 26);
                idx = idx / 26 - 1;
            } while (idx >= 0);
            return res;
        }

        public string AllocateName(string niceName)
        {
            if (CompressFileNames)
            {
                string extension = PathUtils.GetExtension(niceName);
                if (extension != "")
                    extension = "." + extension;
                int idx = 0;
                Extension2LastNameIdx.TryGetValue(extension, out idx);
                do
                {
                    niceName = ToShortName(idx) + extension;
                    idx++;
                    if (OutputSubDir != null)
                        niceName = OutputSubDir + "/" + niceName;
                }
                while (TakenNames.Contains(niceName));
                Extension2LastNameIdx[extension] = idx;
            }
            else
            {
                if (OutputSubDir != null)
                    niceName = OutputSubDir + "/" + niceName;
                int counter = 0;
                string extension = PathUtils.GetExtension(niceName);
                if (extension != "")
                    extension = "." + extension;
                string prefix = niceName.Substring(0, niceName.Length - extension.Length);
                while (TakenNames.Contains(niceName))
                {
                    counter++;
                    niceName = prefix + counter.ToString() + extension;
                }
            }
            TakenNames.Add(niceName);
            return niceName;
        }

        public void SpriterInitialization()
        {
            if (SpriteGeneration && SpriteGenerator == null)
            {
                SpriteGenerator = new SpriteHolder(Owner.DiskCache);
                BundlePngUrl = AllocateName("bundle.png");
            }
            if (BundleJsUrl == null)
                BundleJsUrl = AllocateName("bundle.js");
        }

        public void DetectBobrilJsxDts()
        {
            if (!BobrilJsx)
            {
                BobrilJsxDts = null;
                return;
            }
            var item = Owner.DiskCache.TryGetItem(PathUtils.Join(Owner.Owner.FullPath, "node_modules/bobril/jsx.d.ts"));
            if (item is IFileCache)
            {
                BobrilJsxDts = item.FullPath;
            }
            else
            {
                BobrilJsx = false;
            }
        }

        public void RefreshExampleSources()
        {
            var res = new List<string>(ExampleSources?.Count ?? 1);
            if (Example == "")
            {
                var item = (Owner.Owner.TryGetChild("example.ts") ?? Owner.Owner.TryGetChild("example.tsx")) as IFileCache;
                if (item != null)
                {
                    res.Add(item.FullPath);
                }
            }
            else
            {
                var examplePath = PathUtils.Join(Owner.Owner.FullPath, Example);
                var item = Owner.DiskCache.TryGetItem(examplePath);
                if (item is IDirectoryCache)
                {
                    foreach (var child in (IDirectoryCache)item)
                    {
                        if (!(child is IFileCache))
                            continue;
                        if (child.IsInvalid)
                            continue;
                        var fn = child.FullPath;
                        if (fn.EndsWith(".d.ts"))
                            continue;
                        if (fn.EndsWith(".ts") || fn.EndsWith(".tsx") || fn.EndsWith(".js") || fn.EndsWith(".jsx"))
                            res.Add(fn);
                    }
                }
                else if (item is IFileCache)
                {
                    res.Add(item.FullPath);
                }
            }
            ExampleSources = res;
        }

        public void RefreshTestSources()
        {
            JasmineDts = Tools.JasmineDtsPath;
            var res = new List<string>(TestSources?.Count ?? 4);
            if (TestSourcesRegExp != null)
            {
                var fileRegex = new Regex(TestSourcesRegExp, RegexOptions.CultureInvariant);
                RecursiveFileSearch(Owner.Owner, Owner.DiskCache, fileRegex, res);
            }
            TestSources = res;
        }

        void RecursiveFileSearch(IDirectoryCache owner, IDiskCache diskCache, Regex fileRegex, List<string> res)
        {
            diskCache.UpdateIfNeeded(owner);
            if (owner.IsInvalid)
                return;
            foreach (var item in owner)
            {
                if (item is IDirectoryCache)
                {
                    if (item.Name == "node_modules")
                        continue;
                    if (item.IsInvalid)
                        continue;
                    RecursiveFileSearch(item as IDirectoryCache, diskCache, fileRegex, res);
                }
                else if (item is IFileCache)
                {
                    if (fileRegex.IsMatch(item.Name))
                    {
                        res.Add(item.FullPath);
                    }
                }
            }
        }

        public void FillOutputByAdditionalResourcesDirectory(Dictionary<string, object> filesContent)
        {
            if (AdditionalResourcesDirectory == null)
                return;
            var resourcesPath = PathUtils.Join(Owner.Owner.FullPath, AdditionalResourcesDirectory);
            var item = Owner.DiskCache.TryGetItem(resourcesPath);
            if (item is IDirectoryCache)
            {
                RecursiveFillOutputByAdditionalResourcesDirectory(item as IDirectoryCache, resourcesPath, filesContent);
            }
        }

        void RecursiveFillOutputByAdditionalResourcesDirectory(IDirectoryCache directoryCache, string resourcesPath, Dictionary<string, object> filesContent)
        {
            Owner.DiskCache.UpdateIfNeeded(directoryCache);
            foreach (var child in directoryCache)
            {
                if (child is IDirectoryCache)
                {
                    RecursiveFillOutputByAdditionalResourcesDirectory(child as IDirectoryCache, resourcesPath, filesContent);
                }
                if (child.IsInvalid)
                    continue;
                var outPathFileName = PathUtils.Subtract(child.FullPath, resourcesPath);
                TakenNames.Add(outPathFileName);
                filesContent[outPathFileName] = new Lazy<object>(() =>
                {
                    return (child as IFileCache).ByteContent;
                });
            }
        }
    }
}
