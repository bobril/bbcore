using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JavaScriptEngineSwitcher.Core;
using Lib.Registry;
using Lib.Utils;
using Lib.Utils.Logger;
using Newtonsoft.Json.Linq;

namespace Lib.ToolsDir
{
    public class ToolsDir : IToolsDir
    {
        readonly ILogger _logger;
        static object lockInitialization = new object();

        static object _lock = new object();

        public ToolsDir(string dir, ILogger logger)
        {
            _logger = logger;
            lock (lockInitialization)
            {
                Path = dir;
                if (!new DirectoryInfo(dir).Exists)
                    Directory.CreateDirectory(dir);
                lock (_lock)
                {
                    var jsEngineSwitcher = JsEngineSwitcher.Current;
                    if (!jsEngineSwitcher.EngineFactories.Any())
                    {
                        jsEngineSwitcher.EngineFactories.Add(
                            new JavaScriptEngineSwitcher.ChakraCore.ChakraCoreJsEngineFactory(
                                new JavaScriptEngineSwitcher.ChakraCore.ChakraCoreSettings {MaxStackSize = 2000000}));
                        jsEngineSwitcher.DefaultEngineName =
                            JavaScriptEngineSwitcher.ChakraCore.ChakraCoreJsEngine.EngineName;
                    }
                }

                LoaderJs = ResourceUtils.GetText("Lib.ToolsDir.loader.js");
                JasmineCoreJs = ResourceUtils.GetText("Lib.ToolsDir.jasmine.js");
                JasmineDts = ResourceUtils.GetText("Lib.ToolsDir.jasmine.d.ts");
                JasmineDtsPath = PathUtils.Join(Path, "jasmine.d.ts");
                File.WriteAllText(JasmineDtsPath, JasmineDts);
                JasmineBootJs = ResourceUtils.GetText("Lib.ToolsDir.jasmine-boot.js");
                WebtAJs = ResourceUtils.GetText("Lib.ToolsDir.webt_a.js");
                WebtIndexHtml = ResourceUtils.GetText("Lib.ToolsDir.webt_index.html");
                WebAJs = ResourceUtils.GetText("Lib.ToolsDir.web_a.js");
                WebIndexHtml = ResourceUtils.GetText("Lib.ToolsDir.web_index.html");
                _localeDefs = JObject.Parse(ResourceUtils.GetText("Lib.ToolsDir.localeDefs.json"));
                TsLibSource = ResourceUtils.GetText("Lib.TSCompiler.tslib.js");
                ImportSource = ResourceUtils.GetText("Lib.TSCompiler.import.js");
                LiveReloadJs = ResourceUtils.GetText("Lib.ToolsDir.liveReload.js");
            }
        }

        public string Path { get; }
        public string TypeScriptLibDir { get; private set; }
        public string TypeScriptVersion { get; private set; }

        string _typeScriptJsContent;

        public string TypeScriptJsContent
        {
            get
            {
                if (_typeScriptJsContent == null)
                {
                    _typeScriptJsContent = File.ReadAllText(PathUtils.Join(TypeScriptLibDir, "typescript.js"));

                    // Patch TypeScript compiler to never generate useless __esmodule = true
                    _typeScriptJsContent =
                        _typeScriptJsContent.Replace("(shouldEmitUnderscoreUnderscoreESModule())", "(false)");
                    // Patch https://github.com/Microsoft/TypeScript/commit/c557131cac4379fc3e685514d44b6b82f1f642fb
                    var bugPos = _typeScriptJsContent.IndexOf(
                        "// As the type information we would attempt to lookup to perform ellision is potentially unavailable for the synthesized nodes");
                    if (bugPos > 0)
                    {
                        var bugPos3 = _typeScriptJsContent.IndexOf("visitEachChild", bugPos);
                        var bugPos2 = _typeScriptJsContent.IndexOf("return node;", bugPos);
                        // but only when it is already not fixed
                        if (bugPos3 < 0 || bugPos3 > bugPos2)
                        {
                            _typeScriptJsContent = _typeScriptJsContent.Insert(bugPos2,
                                "if (node.transformFlags & 2) { return ts.visitEachChild(node, visitor, context); };");
                        }
                    }

                    // Patch
                    bugPos = _typeScriptJsContent.IndexOf("function checkUnusedClassMembers(");
                    var bugPos22 = bugPos < 0
                        ? -1
                        : _typeScriptJsContent.IndexOf("case 158 /* IndexSignature */:", bugPos);
                    var bugPos33 = bugPos22 < 0 ? -1 : _typeScriptJsContent.IndexOf("case 207", bugPos22);
                    if (bugPos22 > 0 && (bugPos33 < 0 || bugPos33 > bugPos22 + 200))
                    {
                        _typeScriptJsContent = _typeScriptJsContent.Insert(bugPos22, "case 207:");
                    }

                    // Patch https://github.com/Microsoft/TypeScript/issues/22403 in 2.8.1
                    bugPos = _typeScriptJsContent.IndexOf(
                        "if (links.target && links.target !== unknownSymbol && links.target !== resolvingSymbol) {");
                    if (bugPos > 0)
                    {
                        var bugPos2a =
                            _typeScriptJsContent.IndexOf(
                                "links.nameType = getLiteralTypeFromPropertyName(links.target);", bugPos);
                        if (bugPos2a > 0 && bugPos2a - bugPos < 400)
                        {
                            _typeScriptJsContent = _typeScriptJsContent.Remove(bugPos, bugPos2a - bugPos);
                            _typeScriptJsContent = _typeScriptJsContent.Insert(bugPos,
                                "if (links.target && links.target !== unknownSymbol && links.target !== resolvingSymbol && links.target.escapedName === prop.escapedName) {");
                        }
                    }
                }

                return _typeScriptJsContent;
            }
        }

        public string LoaderJs { get; }

        public string JasmineCoreJs { get; }

        public string JasmineBootJs { get; }

        public string JasmineDts { get; }

        public string JasmineDtsPath { get; }

        public string WebtIndexHtml { get; }
        public string WebAJs { get; }
        public string WebIndexHtml { get; }

        readonly JObject _localeDefs;

        public string WebtAJs { get; }

        public string TsLibSource { get; }

        public string ImportSource { get; }

        public string LiveReloadJs { get; }

        public async Task DownloadAndExtractTS(string dir, string versionString)
        {
            _logger.Info($"Downloading and extracting TypeScript {versionString}");
            var version = new SemVer.Version(versionString);
            var npmr = new NpmRepositoryAccessor();
            var packageEtagAndContent = await npmr.GetPackageInfo("typescript", null);
            var packageInfo = new PackageInfo(packageEtagAndContent.content);
            var task = null as Task<byte[]>;
            packageInfo.LazyParseVersions(v => v == version, reader =>
            {
                var j = PackageJson.Parse(reader);
                var tgzName = PathUtils.SplitDirAndFile(j.Dist.Tarball).Item2;
                _logger.Info($"Downloading Tarball {tgzName}");
                task = npmr.GetPackageTgz("typescript", tgzName);
            });
            if (task != null)
            {
                var bytes = await task;
                _logger.Info($"Extracting {bytes.Length} bytes");
                await TarExtractor.ExtractTgzAsync(bytes, async (name, stream, size) =>
                {
                    if (name.StartsWith("package/"))
                        name = name.Substring("package/".Length);
                    var fn = PathUtils.Join(dir, name);
                    Directory.CreateDirectory(PathUtils.Parent(fn));
                    using (var targetStream = File.Create(fn))
                    {
                        var buf = new byte[4096];
                        while (size > 0)
                        {
                            var read = await stream.ReadAsync(buf, 0, (int) Math.Min((ulong) buf.Length, size));
                            if (read == 0) throw new IOException($"Reading {name} failed");
                            await targetStream.WriteAsync(buf, 0, read);
                            size -= (ulong) read;
                        }
                    }

                    return true;
                });
            }
            else
            {
                throw new Exception($"TypeScript version {version} does not exists");
            }
        }

        public void SetTypeScriptVersion(string version)
        {
            if (TypeScriptVersion == version)
                return;
            _typeScriptJsContent = null;
            var tsVerDir = PathUtils.Join(Path, $"TS{version}");
            var tspackage = PathUtils.Join(tsVerDir, "package.json");
            lock (_lock)
            {
                if (!File.Exists(tspackage))
                {
                    Directory.CreateDirectory(tsVerDir);
                    DownloadAndExtractTS(tsVerDir, version).Wait();
                }
            }

            TypeScriptLibDir = PathUtils.Join(tsVerDir, "lib");
            TypeScriptVersion = version;
        }

        public void SetTypeScriptPath(string projectPath)
        {
            if (TypeScriptVersion == "project")
                return;
            _typeScriptJsContent = null;
            TypeScriptLibDir = PathUtils.Join(projectPath, "node_modules/typescript/lib");
            TypeScriptVersion = "project";
        }

        public IJsEngine CreateJsEngine()
        {
            var jsEngineSwitcher = JsEngineSwitcher.Current;
            return jsEngineSwitcher.CreateDefaultEngine();
        }

        public string GetLocaleDef(string locale)
        {
            while (true)
            {
                if (_localeDefs.TryGetValue(locale, StringComparison.InvariantCultureIgnoreCase, out var val))
                {
                    return val.ToString();
                }

                var dashIndex = locale.IndexOf('-');
                if (dashIndex < 0)
                    return null;
                locale = locale.Substring(0, dashIndex);
            }
        }
    }
}
