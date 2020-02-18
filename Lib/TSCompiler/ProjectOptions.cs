using System;
using System.Collections.Generic;
using System.Globalization;
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
using Njsast.Ast;
using Njsast.Bobril;
using Njsast.Coverage;
using Njsast.Reader;
using Njsast.Runtime;
using Njsast.SourceMap;

namespace Lib.TSCompiler
{
    public class ProjectOptions
    {
        public const string DefaultTypeScriptVersion = "3.7.4";

        public IToolsDir Tools;
        public TSProject Owner;
        public string TestSourcesRegExp;
        public Dictionary<string, AstToplevel>? Defines;
        public Dictionary<string, AstToplevel>? ProcessEnvs;
        public string Title;
        public string HtmlHead;
        public StyleDefNamingStyle StyleDefNaming;
        public string PrefixStyleNames;
        public string Example;
        public bool BobrilJsx;
        public TSCompilerOptions CompilerOptions;
        public string AdditionalResourcesDirectory;
        public bool SpriteGeneration;
        public SpriteHolder SpriteGenerator;
        public string BundlePngUrl;
        public bool GenerateSpritesTs;
        public string Variant;
        public bool NoHtml;
        public bool WarningsAsErrors;
        public bool PreserveProjectRoot;
        public string JasmineVersion;
        public List<string> TestDirectories;
        public string PathToTranslations;
        public bool TsconfigUpdate;
        public Dictionary<string, string?>? BrowserResolve;
        public string? ProxyUrl;

        public Dictionary<string, string> ExpandedProcessEnvs;
        public Dictionary<string, AstNode> ExpandedDefines;
        public string MainFile;
        public string JasmineDts;
        public List<string> TestSources;
        public List<string> ExampleSources;
        public bool LiveReloadEnabled;
        public bool CoverageEnabled;
        public string TypeScriptVersion;
        public string BuildOutputDir;

        public bool Localize;
        public string DefaultLanguage;
        public DepedencyUpdate DependencyUpdate;
        public int LiveReloadIdx;
        public RefDictionary<string, ProjectOptions?> SubProjects;

        public TranslationDb TranslationDb;

        internal string NpmRegistry;

        public TaskCompletionSource<Unit> LiveReloadAwaiter = new TaskCompletionSource<Unit>();
        public IBuildCache BuildCache;
        internal uint ConfigurationBuildCacheId;
        public bool Debug = true;

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

        public void SpriterInitialization(MainBuildResult buildResult)
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
                    foreach (var child in (IDirectoryCache) item)
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
                        var dc =
                            Owner.DiskCache.TryGetItem(PathUtils.Join(Owner.Owner.FullPath, dir)) as IDirectoryCache;
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

        public void FillOutputByAdditionalResourcesDirectory(Dictionary<string, TSProject> buildResultModules,
            MainBuildResult buildResult)
        {
            var nodeModulesDir = Owner.Owner.FullPath;
            Owner.FillOutputByAssets(buildResult, nodeModulesDir, this);
            FillOutputByAssetsFromModules(buildResult, buildResultModules, nodeModulesDir);
            if (AdditionalResourcesDirectory == null)
                return;
            var resourcesPath = PathUtils.Join(Owner.Owner.FullPath, AdditionalResourcesDirectory);
            var item = Owner.DiskCache.TryGetItem(resourcesPath);
            if (item is IDirectoryCache)
            {
                RecursiveFillOutputByAdditionalResourcesDirectory(buildResult, item as IDirectoryCache, resourcesPath);
            }
        }

        void RecursiveFillOutputByAdditionalResourcesDirectory(MainBuildResult buildResult,
            IDirectoryCache directoryCache, string resourcesPath)
        {
            Owner.DiskCache.UpdateIfNeeded(directoryCache);
            foreach (var child in directoryCache)
            {
                if (child is IDirectoryCache)
                {
                    RecursiveFillOutputByAdditionalResourcesDirectory(buildResult, child as IDirectoryCache,
                        resourcesPath);
                    continue;
                }

                if (child.IsInvalid)
                    continue;
                var outPathFileName = PathUtils.Subtract(child.FullPath, resourcesPath);
                buildResult.TakenNames.Add(outPathFileName);
                if (child is IFileCache)
                {
                    buildResult.FilesContent.GetOrAddValueRef(outPathFileName) =
                        new Lazy<object>(() =>
                        {
                            var res = ((IFileCache) child).ByteContent;
                            ((IFileCache) child).FreeCache();
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
                downlevelIteration = true,
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
                allowSyntheticDefaultImports = true,
                lib = GetDefaultTSLibs()
            };
        }

        HashSet<string> GetDefaultTSLibs()
        {
            if (Variant == "worker")
                return new HashSet<string>
                {
                    "es5", "es2015.core", "es2015.promise", "es2015.iterable", "es2015.generator", "es2015.collection",
                    "webworker",
                    "webworker.importscripts"
                };
            if (Variant == "serviceworker")
                return new HashSet<string>
                {
                    "es2017", "webworker"
                };
            return new HashSet<string>
            {
                "es5", "dom", "es2015.core", "es2015.promise", "es2015.iterable", "es2015.generator",
                "es2015.collection"
            };
        }

        public static readonly Regex ResourceLinkDetector = new Regex("<<[^>]+>>", RegexOptions.Compiled);

        public string ExpandHtmlHead(BuildResult buildResult)
        {
            return ResourceLinkDetector.Replace(HtmlHead,
                m =>
                {
                    var fn = PathUtils.Join(Owner.Owner.FullPath, m.Value.Substring(2, m.Length - 4));
                    return buildResult.ToOutputUrl(fn);
                });
        }

        string _originalContent;
        internal string[] IncludeSources;
        internal bool TypeScriptVersionOverride;
        public HashSet<int> IgnoreDiagnostic;
        public string ObsoleteMessage;
        public ITSCompilerOptions FinalCompilerOptions;
        public bool ForbiddenDependencyUpdate;
        public CoverageInstrumentation? CoverageInstrumentation;

        public void UpdateTSConfigJson()
        {
            if (TsconfigUpdate == false)
            {
                return;
            }

            if (SubProjects != null)
            {
                foreach (var (name, subProject) in SubProjects)
                {
                    subProject.TsconfigUpdate = true;
                    subProject.UpdateTSConfigJson();
                }
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
                    .Merge(new TSCompilerOptions {allowJs = false})
                    .Merge(CompilerOptions),
                files = new List<string>(2 + IncludeSources?.Length ?? 0),
                include = new List<string> {"**/*"}
            };

            if ((TestSources?.Count ?? 0) > 0)
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

        public void InitializeTranslationDb(string? specificPath = null)
        {
            if (TranslationDb != null)
            {
                TranslationDb.AddLanguage(DefaultLanguage ?? "en-us");
                return;
            }

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
            var browserValue = parsed?.GetValue("browser");
            BrowserResolve = null;
            if (browserValue != null && browserValue.Type == JTokenType.Object)
            {
                BrowserResolve = browserValue.ToObject<Dictionary<string, object>>(). ToDictionary(p=>p.Key,p=>p.Value as string);
            }

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
                TypeScriptVersion = DefaultTypeScriptVersion;
            }

            Variant = GetStringProperty(bobrilSection, "variant", "");
            JasmineVersion = GetStringProperty(bobrilSection, "jasmineVersion", "330");
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
            BuildOutputDir = GetStringProperty(bobrilSection, "buildOutputDir", null);
            Defines = ParseDefines(bobrilSection?.GetValue("defines") as JObject);
            if (!Defines!.ContainsKey("DEBUG"))
            {
                Defines!["DEBUG"] = Parser.Parse("DEBUG");
            }

            ProcessEnvs = ParseDefines(bobrilSection?.GetValue("envs") as JObject);
            if (!ProcessEnvs!.ContainsKey("NODE_ENV"))
            {
                ProcessEnvs!["NODE_ENV"] = Parser.Parse("DEBUG?\"development\":\"production\"");
            }
            PreserveProjectRoot = bobrilSection?["preserveProjectRoot"]?.Value<bool>() ?? false;
            ProxyUrl = GetStringProperty(bobrilSection, "proxyUrl", null);
        }

        Dictionary<string, AstToplevel> ParseDefines(JObject? jObject)
        {
            var res = new Dictionary<string, AstToplevel>();
            if (jObject != null)
                foreach (var (key, value) in jObject)
                {
                    if (value.Type != JTokenType.String) continue;
                    res[key] = Parser.Parse((string) ((JValue) value).Value);
                }

            return res;
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
                return (string) value;
            return @default;
        }

        public void FillOutputByAssetsFromModules(MainBuildResult buildResult,
            Dictionary<string, TSProject> modules, string nodeModulesDir)
        {
            foreach (var keyValuePair in modules)
            {
                keyValuePair.Value.FillOutputByAssets(buildResult, nodeModulesDir, this);
            }
        }

        public void ApplySourceInfo(ISourceReplacer sourceReplacer, SourceInfo sourceInfo, BuildResult buildResult)
        {
            if (sourceInfo == null) return;

            if (sourceInfo.ProcessEnvs != null)
            {
                foreach (var processEnv in sourceInfo.ProcessEnvs)
                {
                    if (processEnv.Name == null) continue;
                    if (!ExpandedProcessEnvs.TryGetValue(processEnv.Name, out var value))
                    {
                        value = "undefined";
                    }
                    sourceReplacer.Replace(processEnv.StartLine, processEnv.StartCol, processEnv.EndLine,
                        processEnv.EndCol, value);
                }
            }

            if (sourceInfo.Assets != null)
            {
                foreach (var a in sourceInfo.Assets)
                {
                    if (a.Name == null)
                        continue;
                    var assetName = a.Name;
                    if (assetName.StartsWith("project:"))
                    {
                        assetName = assetName.Substring(8);
                        sourceReplacer.Replace(a.StartLine, a.StartCol, a.EndLine, a.EndCol,
                            "\"" + buildResult.SubBuildResults.GetOrFakeValueRef(assetName).BundleJsUrl + "\"");
                        continue;
                    }

                    if (assetName.StartsWith("resource:"))
                    {
                        assetName = assetName.Substring(9);
                    }

                    sourceReplacer.Replace(a.StartLine, a.StartCol, a.EndLine, a.EndCol,
                        "\"" + buildResult.ToOutputUrl(assetName) + "\"");
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
                            sourceReplacer.Replace(s.StartLine, s.StartCol, s.ColorStartLine, s.ColorStartCol,
                                sourceInfo.BobrilImport + ".spritebc(");
                            // Replace parameters after color with sprite size and position
                            sourceReplacer.Replace(s.ColorEndLine, s.ColorEndCol, s.EndLine, s.EndCol,
                                "," + os.owidth + "," + os.oheight + "," + os.ox + "," + os.oy + ")");
                        }
                        else
                        {
                            // Modify method name to b.spriteb and replace parameters with sprite size and position
                            sourceReplacer.Replace(s.StartLine, s.StartCol, s.EndLine, s.EndCol,
                                sourceInfo.BobrilImport + ".spriteb(" + os.owidth + "," + os.oheight + "," + os.ox +
                                "," + os.oy + ")");
                        }
                    }
                }
                else
                {
                    foreach (var s in sourceInfo.Sprites)
                    {
                        if (s.Name == null)
                            continue;
                        sourceReplacer.Replace(s.NameStartLine, s.NameStartCol, s.NameEndLine, s.NameEndCol,
                            "\"" + buildResult.ToOutputUrl(s.Name) + "\"");
                    }
                }
            }

            var trdb = TranslationDb;
            if (trdb != null && sourceInfo.VdomTranslations != null)
            {
                foreach (var t in sourceInfo.VdomTranslations)
                {
                    if (t.Message == null)
                        continue;
                    var repls = t.Replacements;
                    if (repls == null)
                        continue;
                    var id = trdb.AddToDB(t.Message, t.Hint, true);
                    var finalId = trdb.MapId(id);
                    foreach (var rep in repls)
                    {
                        if (rep.Type == SourceInfo.ReplacementType.MoveToPlace)
                        {
                            sourceReplacer.Move(rep.StartLine, rep.StartCol, rep.EndLine, rep.EndCol, rep.PlaceLine,
                                rep.PlaceCol);
                            continue;
                        }

                        var tt = rep.Text;
                        if (rep.Type == SourceInfo.ReplacementType.MessageId)
                        {
                            tt = "" + finalId;
                        }

                        sourceReplacer.Replace(rep.StartLine, rep.StartCol, rep.EndLine, rep.EndCol, tt ?? "");
                    }
                }
            }

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
                                    sourceReplacer.Replace(s.StartLine, s.StartCol, s.EndLine, s.EndCol,
                                        "\"" + styleDefPrefix + s.Name + "\"");
                                }
                                else
                                {
                                    sourceReplacer.Replace(s.StartLine, s.StartCol, s.StartLine, s.StartCol,
                                        "\"" + styleDefPrefix + "\"+(");
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
                            sourceReplacer.Replace(s.BeforeNameLine, s.BeforeNameCol, s.BeforeNameLine, s.BeforeNameCol,
                                padArgs + ",\"" + styleDefPrefix + s.Name + "\"");
                        }
                    }
                }
            }
        }

        public void ExpandEnv()
        {
            var constsInput = new Dictionary<string, AstNode>();
            var definesOutput = new Dictionary<string, AstNode>();
            constsInput["DEBUG"] = Debug ? (AstNode) AstTrue.Instance : AstFalse.Instance;
            foreach (var (key, value) in Defines)
            {
                var clone = value.DeepClone();
                clone.FigureOutScope();
                clone = (AstToplevel) new EnvExpanderTransformer(constsInput, GetSystemEnvValue)
                    .Transform(clone);
                definesOutput[key] = TypeConverter.ToAst((clone.Body.Last as AstSimpleStatement)?.Body.ConstValue());
            }

            var envReplace = new Dictionary<string, string>();
            foreach (var (key, value) in ProcessEnvs)
            {
                var clone = value.DeepClone();
                clone.FigureOutScope();
                clone = (AstToplevel) new EnvExpanderTransformer(definesOutput, GetSystemEnvValue)
                    .Transform(clone);
                envReplace[key] = TypeConverter.ToAst((clone.Body.Last as AstSimpleStatement)?.Body.ConstValue())
                    .PrintToString();
            }

            ExpandedDefines = definesOutput;
            ExpandedProcessEnvs = envReplace;
        }

        static string? GetSystemEnvValue(string arg)
        {
            return Environment.GetEnvironmentVariable(arg);
        }

        public string GetDefaultBundleJsName()
        {
            return Variant switch
            {
                "serviceworker" => "sw.js",
                "worker" => "worker.js",
                _ => "bundle.js"
            };
        }

        public void InitializeLocalizationAndUpdateTsLintJson()
        {
            if (Localize)
            {
                InitializeTranslationDb();
            }

            var bbTslint = Owner.DevDependencies?.FirstOrDefault(s => s.StartsWith("bb-tslint"));
            if (bbTslint != null)
            {
                var srcTsLint = PathUtils.Join(Owner.Owner.FullPath, $"node_modules/{bbTslint}/tslint.json");
                var srcFile = Owner.DiskCache.TryGetItem(srcTsLint) as IFileCache;
                var dstTsLint = PathUtils.Join(Owner.Owner.FullPath, "tslint.json");
                if (srcFile != null && (!(Owner.DiskCache.TryGetItem(dstTsLint) is IFileCache dstFile) ||
                                        !dstFile.HashOfContent.SequenceEqual(srcFile.HashOfContent)))
                {
                    File.WriteAllBytes(dstTsLint, srcFile.ByteContent);
                    Console.WriteLine($"Updated tslint.json from {srcTsLint}");
                }
            }
        }

        public void UpdateFromProjectJson(bool? localizeValue)
        {
            Owner.LoadProjectJson(ForbiddenDependencyUpdate, this);
            if (localizeValue.HasValue)
                Localize = localizeValue.Value;
            InitializeLocalizationAndUpdateTsLintJson();
            Owner.UsedDependencies = new HashSet<string>();
        }

        public IReadOnlyDictionary<string, object> BuildDefines(MainBuildResult mainBuildResult)
        {
            var res = new Dictionary<string, object>();
            if (Variant == "serviceworker")
            {
                var buildDate = DateTime.UtcNow;
                res.Add("swBuildDate", buildDate.ToString("O"));
                res.Add("swBuildId", MainBuildResult.ToShortName(buildDate.Ticks));
                res.Add("swFiles", mainBuildResult.FilesContent.Select(a => a.Key).OrderBy(a => a).ToArray());
            }

            foreach (var p in ExpandedDefines)
            {
                res.Add(p.Key, p.Value.ConstValue());
            }

            return res;
        }
    }
}
