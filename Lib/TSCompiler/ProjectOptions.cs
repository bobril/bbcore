using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BTDB.Collections;
using Lib.AssetsPlugin;
using Lib.BuildCache;
using Lib.DiskCache;
using Lib.ToolsDir;
using Lib.Translation;
using Lib.Utils;
using Lib.Utils.Logger;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Lib.TSCompiler
{
    public class ProjectOptions
    {
        public IToolsDir Tools;
        public TSProject Owner;
        public string TestSourcesRegExp;
        public Dictionary<string, bool> Defines;
        public string Title;
        public string HtmlHead;
        public StyleDefNamingStyle StyleDefNaming;
        public string PrefixStyleNames;
        public string Example;
        public bool BobrilJsx;
        public TSCompilerOptions CompilerOptions;
        public string AdditionalResourcesDirectory;
        public string CommonSourceDirectory;
        public bool SpriteGeneration;
        public SpriteHolder SpriteGenerator;
        public string BundlePngUrl;
        public string BundleJsUrl;
        public bool GenerateSpritesTs;
        public string Variant;
        public bool NoHtml;
        public bool WarningsAsErrors;
        public string JasmineVersion;
        public List<string> TestDirectories;

        public string HtmlHeadExpanded;
        public string MainFile;
        public Dictionary<string, string> MainFileVariants;
        public string JasmineDts;
        public List<string> TestSources;
        public List<string> ExampleSources;
        public string BobrilJsxDts;
        public FastBundleBundler MainProjFastBundle;
        public FastBundleBundler TestProjFastBundle;
        public bool LiveReloadEnabled;
        public bool AllowModuleDeepImport;
        public string TypeScriptVersion;

        public bool Localize;
        public string DefaultLanguage;
        public DepedencyUpdate DependencyUpdate;
        public string OutputSubDir;
        public bool CompressFileNames;
        public bool BundleCss;
        public int LiveReloadIdx;

        public TranslationDb TranslationDb;

        // value could be string or byte[]
        public RefDictionary<string, object> FilesContent;
        internal string NpmRegistry;

        public Dictionary<string, int> Extension2LastNameIdx = new Dictionary<string, int>();
        public HashSet<string> TakenNames = new HashSet<string>();
        public TaskCompletionSource<Unit> LiveReloadAwaiter = new TaskCompletionSource<Unit>();
        public IBuildCache BuildCache;
        internal uint ConfigurationBuildCacheId;

        public void RefreshMainFile()
        {
            MainFile = PathUtils.Join(Owner.Owner.FullPath, Owner.MainFile);
            if (!(Owner.DiskCache.TryGetItem(MainFile) is IFileCache))
            {
                Owner.Logger.Warn("Main file " + MainFile + " not found");
            }
        }

        string ToShortName(int idx)
        {
            Span<char> res = new char[8];
            var resLen = 0;
            do
            {
                res[resLen++] = (char)(97 + idx % 26);
                idx = idx / 26 - 1;
            } while (idx >= 0);

            return new string(res.Slice(0, resLen));
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
                        niceName = $"{OutputSubDir}/{niceName}";
                } while (TakenNames.Contains(niceName));

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
                var item =
                    (Owner.Owner.TryGetChild("example.ts", true) ?? Owner.Owner.TryGetChild("example.tsx", true)) as IFileCache;
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

        public void GenerateCode()
        {
            var assetsPlugin = new AssetsGenerator(Owner.DiskCache);
            if (assetsPlugin.Run(Owner.Owner.FullPath, GenerateSpritesTs))
            {
                Owner.DiskCache.CheckForTrueChange();
                Owner.DiskCache.ResetChange();
            }
        }

        public void RefreshTestSources()
        {
            Tools.SetJasmineVersion(JasmineVersion);
            JasmineDts = Tools.JasmineDtsPath;
            var res = new List<string>(TestSources?.Count ?? 4);
            if (TestSourcesRegExp != null)
            {
                var fileRegex = new Regex(TestSourcesRegExp, RegexOptions.CultureInvariant);
                if (TestDirectories != null)
                {
                    foreach (var dir in TestDirectories)
                    {
                        var dc = Owner.DiskCache.TryGetItem(PathUtils.Join(Owner.Owner.FullPath, dir)) as IDirectoryCache;
                        if (dc != null && !dc.IsInvalid)
                        {
                            RecursiveFileSearch(dc, Owner.DiskCache, fileRegex, res);
                        }
                    }
                }
                else
                {
                    RecursiveFileSearch(Owner.Owner, Owner.DiskCache, fileRegex, res);
                }
            }

            res.Sort(StringComparer.Ordinal);
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
                else if (item is IFileCache && !item.IsVirtual)
                {
                    if (item.Name == "jasmine.d.ts")
                    {
                        JasmineDts = item.FullPath;
                    }

                    if (fileRegex.IsMatch(item.Name))
                    {
                        res.Add(item.FullPath);
                    }
                }
            }
        }

        public void FillOutputByAdditionalResourcesDirectory(RefDictionary<string, object> filesContent,
            Dictionary<string, TSProject> buildResultModules)
        {
            var nodeModulesDir = Owner.Owner.FullPath;
            Owner.FillOutputByAssets(filesContent, TakenNames, nodeModulesDir, this);
            FillOutputByAssetsFromModules(filesContent, buildResultModules, nodeModulesDir);
            if (AdditionalResourcesDirectory == null)
                return;
            var resourcesPath = PathUtils.Join(Owner.Owner.FullPath, AdditionalResourcesDirectory);
            var item = Owner.DiskCache.TryGetItem(resourcesPath);
            if (item is IDirectoryCache)
            {
                RecursiveFillOutputByAdditionalResourcesDirectory(item as IDirectoryCache, resourcesPath, filesContent);
            }
        }

        void RecursiveFillOutputByAdditionalResourcesDirectory(IDirectoryCache directoryCache, string resourcesPath,
            RefDictionary<string, object> filesContent)
        {
            Owner.DiskCache.UpdateIfNeeded(directoryCache);
            foreach (var child in directoryCache)
            {
                if (child is IDirectoryCache)
                {
                    RecursiveFillOutputByAdditionalResourcesDirectory(child as IDirectoryCache, resourcesPath,
                        filesContent);
                    continue;
                }

                if (child.IsInvalid)
                    continue;
                var outPathFileName = PathUtils.Subtract(child.FullPath, resourcesPath);
                TakenNames.Add(outPathFileName);
                if (child is IFileCache)
                {
                    filesContent.GetOrAddValueRef(outPathFileName) =
                        new Lazy<object>(() =>
                        {
                            var res = ((IFileCache)child).ByteContent;
                            ((IFileCache)child).FreeCache();
                            return res;
                        });
                }
            }
        }

        public TSCompilerOptions GetDefaultTSCompilerOptions()
        {
            return new TSCompilerOptions
            {
                sourceMap = true,
                skipLibCheck = true,
                skipDefaultLibCheck = true,
                target = ScriptTarget.Es5,
                module = ModuleKind.Commonjs,
                declaration = true,
                preserveConstEnums = false,
                jsx = JsxEmit.React,
                reactNamespace = BobrilJsx ? "b" : "React",
                experimentalDecorators = true,
                noEmitHelpers = true,
                allowJs = true,
                checkJs = false,
                removeComments = false,
                types = null,
                resolveJsonModule = true,
                lib = GetDefaultTSLibs()
            };
        }

        HashSet<string> GetDefaultTSLibs()
        {
            if (Variant == "worker")
                return new HashSet<string>
                {
                    "es5", "es2015.core", "es2015.promise", "es2015.iterable", "es2015.collection", "webworker",
                    "webworker.importscripts"
                };
            if (Variant == "serviceworker")
                return new HashSet<string>
                {
                    "es2017", "webworker"
                };
            return new HashSet<string>
                {"es5", "dom", "es2015.core", "es2015.promise", "es2015.iterable", "es2015.collection"};
        }

        string _originalContent;
        internal string CurrentBuildCommonSourceDirectory;
        internal string[] IncludeSources;
        internal bool TypeScriptVersionOverride;
        public HashSet<int> IgnoreDiagnostic;
        public string ObsoleteMessage;

        public void UpdateTSConfigJson()
        {
            var fsAbstration = Owner.DiskCache.FsAbstraction;
            var tsConfigPath = PathUtils.Join(Owner.Owner.FullPath, "tsconfig.json");
            if (_originalContent == null && fsAbstration.FileExists(tsConfigPath))
            {
                try
                {
                    _originalContent = fsAbstration.ReadAllUtf8(tsConfigPath);
                }
                catch
                {
                }
            }

            var newConfigObject = new TSConfigJson
            {
                compilerOptions = GetDefaultTSCompilerOptions()
                    .Merge(new TSCompilerOptions { allowJs = false })
                    .Merge(CompilerOptions),
                files = new List<string>(2 + IncludeSources?.Length ?? 0),
                include = new List<string> { "**/*" }
            };
            if (BobrilJsx)
            {
                newConfigObject.files.Add(PathUtils.Subtract(BobrilJsxDts, Owner.Owner.FullPath));
            }

            if (TestSources.Count > 0)
            {
                if (Tools.JasmineDtsPath == JasmineDts)
                {
                    newConfigObject.files.Add(Tools.JasmineDtsPath);
                }
            }

            if (IncludeSources != null)
            {
                newConfigObject.files.AddRange(IncludeSources);
            }

            if (newConfigObject.files.Count == 0)
            {
                newConfigObject.files = null;
            }

            var newContent = JsonConvert.SerializeObject(newConfigObject, Formatting.Indented,
                TSCompilerOptions.GetSerializerSettings());
            if (newContent != _originalContent)
            {
                try
                {
                    File.WriteAllText(tsConfigPath, newContent, new UTF8Encoding(false));
                }
                catch
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Writting to " + tsConfigPath + " failed");
                    Console.ForegroundColor = ConsoleColor.Gray;
                }

                _originalContent = newContent;
            }
        }

        public void StoreResultToBuildCache(BuildResult result)
        {
            var bc = BuildCache;
            foreach (var f in result.RecompiledLast)
            {
                if (f.TakenFromBuildCache)
                    continue;
                if ((f.Type != FileCompilationType.TypeScript && f.Type != FileCompilationType.EsmJavaScript) || (f.SourceInfo != null && !f.SourceInfo.IsEmpty) ||
                    f.LocalImports.Count != 0 || f.ModuleImports.Count != 0) continue;
                if (bc.FindTSFileBuildCache(f.Owner.HashOfContent, ConfigurationBuildCacheId) !=
                    null) continue;
                var fbc = new TSFileBuildCache();
                fbc.ConfigurationId = ConfigurationBuildCacheId;
                fbc.ContentHash = f.Owner.HashOfContent;
                fbc.DtsOutput = f.DtsLink?.Owner.Utf8Content;
                fbc.JsOutput = f.Output;
                fbc.MapLink = f.MapLink;
                bc.Store(fbc);
            }
        }

        public void InitializeTranslationDb(string specificPath = null)
        {
            TranslationDb = new TranslationDb(Owner.DiskCache.FsAbstraction, new ConsoleLogger());
            TranslationDb.AddLanguage(DefaultLanguage ?? "en-us");
            if (specificPath == null)
            {
                TranslationDb.LoadLangDbs(PathUtils.Join(Owner.Owner.FullPath, "translations"));
            }
            else TranslationDb.LoadLangDb(specificPath);
        }

        public void FillProjectOptionsFromPackageJson(JObject parsed)
        {
            Localize = Owner.Dependencies?.Contains("bobril-g11n") ?? false;
            TestSourcesRegExp = "^.*?(?:\\.s|S)pec(?:\\.d)?\\.ts(?:x)?$";
            if (parsed?.GetValue("publishConfig") is JObject publishConfigSection)
            {
                NpmRegistry = publishConfigSection.Value<string>("registry");
            }

            var bobrilSection = parsed?.GetValue("bobril") as JObject;
            TypeScriptVersion = GetStringProperty(bobrilSection, "tsVersion", "");
            if (TypeScriptVersion != "")
            {
                TypeScriptVersionOverride = true;
            }
            else
            {
                TypeScriptVersionOverride = false;
                TypeScriptVersion = TSProject.DefaultTypeScriptVersion;
            }

            Variant = GetStringProperty(bobrilSection, "variant", "");
            JasmineVersion = GetStringProperty(bobrilSection, "jasmineVersion", "2.99");
            NoHtml = bobrilSection?["nohtml"]?.Value<bool>() ?? Variant != "";
            Title = GetStringProperty(bobrilSection, "title", "Bobril Application");
            HtmlHead = GetStringProperty(bobrilSection, "head",
                "<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\" />");
            PrefixStyleNames = GetStringProperty(bobrilSection, "prefixStyleDefs", "");
            Example = GetStringProperty(bobrilSection, "example", "");
            AdditionalResourcesDirectory =
                GetStringProperty(bobrilSection, "additionalResourcesDirectory", null);
            BobrilJsx = true;
            CompilerOptions = bobrilSection != null
                ? TSCompilerOptions.Parse(bobrilSection.GetValue("compilerOptions") as JObject)
                : null;
            DependencyUpdate =
                String2DependencyUpdate(GetStringProperty(bobrilSection, "dependencies", "install"));
            var includeSources = bobrilSection?.GetValue("includeSources") as JArray;
            IncludeSources = includeSources?.Select(i => i.ToString()).ToArray();
            if (bobrilSection?.GetValue("ignoreDiagnostic") is JArray ignoreDiagnostic)
                IgnoreDiagnostic = new HashSet<int>(ignoreDiagnostic.Select(i => i.Value<int>()).ToArray());
            var pluginsSection = bobrilSection?.GetValue("plugins") as JObject;
            GenerateSpritesTs =
                pluginsSection?["bb-assets-generator-plugin"]?["generateSpritesFile"]?.Value<bool>() ?? false;
            WarningsAsErrors = bobrilSection?["warningsAsErrors"]?.Value<bool>() ?? false;
            ObsoleteMessage = GetStringProperty(bobrilSection, "obsolete", null);
            AllowModuleDeepImport = bobrilSection?["allowModuleDeepImport"]?.Value<bool>() ?? false;
            TestDirectories = bobrilSection?["testDirectories"]?.Values<string>().ToList();
        }

        static DepedencyUpdate String2DependencyUpdate(string value)
        {
            switch (value.ToLowerInvariant())
            {
                case "disable":
                case "disabled":
                    return DepedencyUpdate.Disabled;
                case "update":
                case "upgrade":
                    return DepedencyUpdate.Upgrade;
                default:
                    return DepedencyUpdate.Install;
            }
        }

        static string GetStringProperty(JObject obj, string name, string @default)
        {
            if (obj != null && obj.TryGetValue(name, out var value) && value.Type == JTokenType.String)
                return (string)value;
            return @default;
        }

        public void FillOutputByAssetsFromModules(RefDictionary<string, object> filesContent,
            Dictionary<string, TSProject> modules, string nodeModulesDir)
        {
            foreach (var keyValuePair in modules)
            {
                keyValuePair.Value.FillOutputByAssets(filesContent, TakenNames, nodeModulesDir, this);
            }
        }
    }
}
