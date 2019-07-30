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
using Njsast.Bobril;
using Njsast.SourceMap;

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
        public bool GenerateSpritesTs;
        public string Variant;
        public bool NoHtml;
        public bool WarningsAsErrors;
        public string JasmineVersion;
        public List<string> TestDirectories;
        public string PathToTranslations;
        public bool TsconfigUpdate;

        public string HtmlHeadExpanded;
        public string MainFile;
        public Dictionary<string, string> MainFileVariants;
        public string JasmineDts;
        public List<string> TestSources;
        public List<string> ExampleSources;
        public bool LiveReloadEnabled;
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

        public TaskCompletionSource<Unit> LiveReloadAwaiter = new TaskCompletionSource<Unit>();
        public IBuildCache BuildCache;
        internal uint ConfigurationBuildCacheId;

        public void RefreshCompilerOptions()
        {
            if (FinalCompilerOptions != null) return;
            var compOpt = GetDefaultTSCompilerOptions();
            compOpt.Merge(CompilerOptions);
            FinalCompilerOptions = compOpt;
        }

        public void RefreshMainFile()
        {
            var res = PathUtils.Join(Owner.Owner.FullPath, Owner.MainFile);
            if (!(Owner.DiskCache.TryGetItem(res) is IFileCache))
            {
                Owner.Logger.Warn("Main file " + res + " not found");
                res = null;
            }
            if (MainFile == null || res == null || MainFile != res)
            {
                MainFile = res;
            }
        }

        public void SpriterInitialization(BuildResult buildResult)
        {
            if (SpriteGeneration && SpriteGenerator == null)
            {
                SpriteGenerator = new SpriteHolder(Owner.DiskCache, Owner.Logger);
                BundlePngUrl = buildResult.AllocateName("bundle.png");
            }
        }

        public void RefreshExampleSources()
        {
            var res = new List<string>(ExampleSources?.Count ?? 1);
            if (Example == "")
            {
                var item =
                    (Owner.Owner.TryGetChild("example.tsx") ?? Owner.Owner.TryGetChild("example.ts")) as IFileCache;
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
                        if (fn.EndsWith(".ts") || fn.EndsWith(".tsx"))
                            res.Add(fn);
                    }
                }
                else if (item is IFileCache)
                {
                    res.Add(item.FullPath);
                }
                res.Sort(StringComparer.Ordinal);
            }

            if (ExampleSources == null || !ExampleSources.SequenceEqual(res))
            {
                ExampleSources = res;
            }
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
            if (TestSources == null || !TestSources.SequenceEqual(res))
            {
                TestSources = res;
            }
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
            Dictionary<string, TSProject> buildResultModules, BuildResult buildResult)
        {
            var nodeModulesDir = Owner.Owner.FullPath;
            Owner.FillOutputByAssets(filesContent, buildResult, nodeModulesDir, this);
            FillOutputByAssetsFromModules(filesContent, buildResultModules, nodeModulesDir, buildResult);
            if (AdditionalResourcesDirectory == null)
                return;
            var resourcesPath = PathUtils.Join(Owner.Owner.FullPath, AdditionalResourcesDirectory);
            var item = Owner.DiskCache.TryGetItem(resourcesPath);
            if (item is IDirectoryCache)
            {
                RecursiveFillOutputByAdditionalResourcesDirectory(item as IDirectoryCache, resourcesPath, filesContent, buildResult);
            }
        }

        void RecursiveFillOutputByAdditionalResourcesDirectory(IDirectoryCache directoryCache, string resourcesPath,
            RefDictionary<string, object> filesContent, BuildResult buildResult)
        {
            Owner.DiskCache.UpdateIfNeeded(directoryCache);
            foreach (var child in directoryCache)
            {
                if (child is IDirectoryCache)
                {
                    RecursiveFillOutputByAdditionalResourcesDirectory(child as IDirectoryCache, resourcesPath,
                        filesContent, buildResult);
                    continue;
                }

                if (child.IsInvalid)
                    continue;
                var outPathFileName = PathUtils.Subtract(child.FullPath, resourcesPath);
                buildResult.TakenNames.Add(outPathFileName);
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
                skipLibCheck = false,
                skipDefaultLibCheck = true,
                target = ScriptTarget.Es5,
                module = ModuleKind.Commonjs,
                declaration = false,
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
        internal string[] IncludeSources;
        internal bool TypeScriptVersionOverride;
        public HashSet<int> IgnoreDiagnostic;
        public string ObsoleteMessage;
        public ITSCompilerOptions FinalCompilerOptions;

        public void UpdateTSConfigJson()
        {
            if (TsconfigUpdate == false)
            {
                return;
            }
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

        public void InitializeTranslationDb(string specificPath = null)
        {
            TranslationDb = new TranslationDb(Owner.DiskCache.FsAbstraction, new ConsoleLogger());
            TranslationDb.AddLanguage(DefaultLanguage ?? "en-us");
            if (specificPath == null)
            {
                TranslationDb.LoadLangDbs(PathUtils.Join(Owner.Owner.FullPath, PathToTranslations ?? "translations"));
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
            TestDirectories = bobrilSection?["testDirectories"]?.Values<string>().ToList();
            Localize = bobrilSection?["localize"]?.Value<bool>() ?? Localize;
            PathToTranslations = GetStringProperty(bobrilSection, "pathToTranslations", null);
            TsconfigUpdate = bobrilSection?["tsconfigUpdate"]?.Value<bool>() ?? true;
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
            Dictionary<string, TSProject> modules, string nodeModulesDir, BuildResult buildResult)
        {
            foreach (var keyValuePair in modules)
            {
                keyValuePair.Value.FillOutputByAssets(filesContent, buildResult, nodeModulesDir, this);
            }
        }

        public void ApplySourceInfo(ISourceReplacer sourceReplacer, SourceInfo sourceInfo, BuildResult buildResult)
        {
            if (sourceInfo == null) return;

            if (sourceInfo.Assets != null)
            {
                foreach (var a in sourceInfo.Assets)
                {
                    if (a.Name == null)
                        continue;
                    var assetName = a.Name;
                    if (assetName.StartsWith("resource:"))
                    {
                        assetName = assetName.Substring(9);
                    }
                    sourceReplacer.Replace(a.StartLine, a.StartCol, a.EndLine, a.EndCol, "\"" + buildResult.ToOutputUrl(assetName) + "\"");
                }
            }

            if (sourceInfo.Sprites != null)
            {
                if (SpriteGeneration)
                {
                    var spriteHolder = SpriteGenerator;
                    var outputSprites = spriteHolder.Retrieve(sourceInfo.Sprites);
                    foreach (var os in outputSprites)
                    {
                        var s = os.Me;
                        if (s.Name == null)
                            continue;
                        if (s.HasColor == true && s.Color == null)
                        {
                            // Modify method name to b.spritebc and remove first parameter with sprite name
                            sourceReplacer.Replace(s.StartLine, s.StartCol, s.ColorStartLine, s.ColorStartCol, sourceInfo.BobrilImport + ".spritebc(");
                            // Replace parameters after color with sprite size and position
                            sourceReplacer.Replace(s.ColorEndLine, s.ColorEndCol, s.EndLine, s.EndCol, "," + os.owidth + "," + os.oheight + "," + os.ox + "," + os.oy + ")");
                        }
                        else
                        {
                            // Modify method name to b.spriteb and replace parameters with sprite size and position
                            sourceReplacer.Replace(s.StartLine, s.StartCol, s.EndLine, s.EndCol, sourceInfo.BobrilImport + ".spriteb(" + os.owidth + "," + os.oheight + "," + os.ox + "," + os.oy + ")");
                        }
                    }
                }
                else
                {
                    foreach (var s in sourceInfo.Sprites)
                    {
                        if (s.Name == null)
                            continue;
                        sourceReplacer.Replace(s.NameStartLine, s.NameStartCol, s.NameEndLine, s.NameEndCol, "\"" + buildResult.ToOutputUrl(s.Name) + "\"");
                    }
                }
            }

            var trdb = TranslationDb;
            if (trdb != null && sourceInfo.Translations != null)
            {
                foreach (var t in sourceInfo.Translations)
                {
                    if (t.Message == null)
                        continue;
                    if (t.JustFormat)
                        continue;
                    var id = trdb.AddToDB(t.Message, t.Hint, t.WithParams);
                    var finalId = trdb.MapId(id);
                    sourceReplacer.Replace(t.StartLine, t.StartCol, t.EndLine, t.EndCol, "" + finalId);
                    sourceReplacer.Replace(t.StartHintLine, t.StartHintCol, t.EndHintLine, t.EndHintCol, null);
                }
            }

            var styleDefs = sourceInfo.StyleDefs;
            if (styleDefs != null)
            {
                var styleDefNaming = StyleDefNaming;
                var styleDefPrefix = PrefixStyleNames;
                foreach (var s in styleDefs)
                {
                    var skipEx = s.IsEx ? 1 : 0;
                    if (s.UserNamed)
                    {
                        if (styleDefNaming == StyleDefNamingStyle.AddNames ||
                            styleDefNaming == StyleDefNamingStyle.PreserveNames)
                        {
                            if (styleDefPrefix.Length > 0)
                            {
                                if (s.Name != null)
                                {
                                    sourceReplacer.Replace(s.StartLine, s.StartCol, s.EndLine, s.EndCol, "\"" + styleDefPrefix + s.Name + "\"");
                                }
                                else
                                {
                                    sourceReplacer.Replace(s.StartLine, s.StartCol, s.StartLine, s.StartCol, "\"" + styleDefPrefix + "\"+(");
                                    sourceReplacer.Replace(s.EndLine, s.EndCol, s.EndLine, s.EndCol, ")");
                                }
                            }
                        }
                        else
                        {
                            sourceReplacer.Replace(s.BeforeNameLine, s.BeforeNameCol, s.EndLine, s.EndCol, "");
                        }
                    }
                    else
                    {
                        if (styleDefNaming == StyleDefNamingStyle.AddNames && s.Name != null)
                        {
                            var padArgs = (s.ArgCount == 1 + (s.IsEx ? 1 : 0)) ? ",undefined" : "";
                            sourceReplacer.Replace(s.BeforeNameLine, s.BeforeNameCol, s.BeforeNameLine, s.BeforeNameCol, padArgs + ",\"" + styleDefPrefix + s.Name + "\"");
                        }
                    }
                }
            }
        }
    }
}
