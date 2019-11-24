using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
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
                                new JavaScriptEngineSwitcher.ChakraCore.ChakraCoreSettings {MaxStackSize = 2000000, DisableFatalOnOOM = true}));
                        jsEngineSwitcher.DefaultEngineName =
                            JavaScriptEngineSwitcher.ChakraCore.ChakraCoreJsEngine.EngineName;
                    }
                }

                SetJasmineVersion("330");
                LoaderJs = ResourceUtils.GetText("Lib.ToolsDir.loader.js").Replace("\"use strict\";","");
                JasmineCoreJs299 = ResourceUtils.GetText("Lib.ToolsDir.jasmine299.js");
                JasmineDts299 = ResourceUtils.GetText("Lib.ToolsDir.jasmine299.d.ts");
                JasmineDtsPath299 = PathUtils.Join(Path, "jasmine.d.ts");
                if (!File.Exists(JasmineDtsPath299) || File.ReadAllText(JasmineDtsPath299) != JasmineDts299)
                    File.WriteAllText(JasmineDtsPath299, JasmineDts299);
                JasmineBootJs299 = ResourceUtils.GetText("Lib.ToolsDir.jasmine-boot299.js");
                JasmineCoreJs330 = ResourceUtils.GetText("Lib.ToolsDir.jasmine330.js");
                JasmineDts330 = ResourceUtils.GetText("Lib.ToolsDir.jasmine330.d.ts");
                JasmineDtsPath330 = PathUtils.Join(Path, "jasmine330.d.ts");
                if (!File.Exists(JasmineDtsPath330) || File.ReadAllText(JasmineDtsPath330) != JasmineDts330)
                    File.WriteAllText(JasmineDtsPath330, JasmineDts330);
                JasmineBootJs330 = ResourceUtils.GetText("Lib.ToolsDir.jasmine-boot330.js");

                WebtZip = ResourceUtils.GetZip("Lib.ToolsDir.webt.zip");
                WebZip = ResourceUtils.GetZip("Lib.ToolsDir.web.zip");
                _localeDefs = JObject.Parse(ResourceUtils.GetText("Lib.ToolsDir.localeDefs.json"));
                LiveReloadJs = ResourceUtils.GetText("Lib.ToolsDir.liveReload.js");
            }
        }

        public IDictionary<string, byte[]> WebZip { get; }

        public IDictionary<string, byte[]> WebtZip { get; }

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

                    // Patch TS to generate d.ts also from node_modules directory (it makes much faster typecheck when changing something in node_modules)
                    // function isSourceFileFromExternalLibrary must return always false!
                    _typeScriptJsContent =
                        _typeScriptJsContent.Replace("return !!sourceFilesFoundSearchingNodeModules.get(file.path);", "return false;");
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

                    // patch https://github.com/microsoft/TypeScript/issues/33142 in 3.6.2
                    _typeScriptJsContent = _typeScriptJsContent.Replace("process.argv", "\"\"");

                    // Remove too defensive check for TS2742 - it is ok in Bobril-build to have relative paths into node_modules when in sandboxes
                    _typeScriptJsContent = _typeScriptJsContent.Replace(".indexOf(\"/node_modules/\") >= 0", "===null");
                }

                return _typeScriptJsContent;
            }
        }

        public string LoaderJs { get; }

        public string JasmineCoreJs299 { get; }

        public string JasmineBootJs299 { get; }

        public string JasmineDts299 { get; }

        public string JasmineDtsPath299 { get; }
        public string JasmineCoreJs330 { get; }

        public string JasmineBootJs330 { get; }
        public string JasmineDts330 { get; }

        public string JasmineDtsPath330 { get; }

        public string JasmineCoreJs => _jasmineVersion == "2.99" ? JasmineCoreJs299 : JasmineCoreJs330;

        public string JasmineBootJs => _jasmineVersion == "2.99" ? JasmineBootJs299 : JasmineBootJs330;

        public string JasmineDts => _jasmineVersion == "2.99" ? JasmineDts299 : JasmineDts330;

        public string JasmineDtsPath => _jasmineVersion == "2.99" ? JasmineDtsPath299 : JasmineDtsPath330;

        readonly JObject _localeDefs;
        string _proxyWeb;
        string _proxyWebt;
        
        public string LiveReloadJs { get; }

        public async Task DownloadAndExtractTS(string dir, string versionString)
        {
            _logger.Info($"Downloading and extracting TypeScript {versionString}");
            var version = new SemVer.Version(versionString);
            var npmr = new NpmRepositoryAccessor();
            var packageEtagAndContent = await npmr.GetPackageInfo("typescript", null);
            var task = null as Task<byte[]>;
            try
            {
                var packageInfo = new PackageInfo(packageEtagAndContent.content);
                packageInfo.LazyParseVersions(v => v == version, reader =>
                {
                    var j = PackageJson.Parse(reader);
                    PathUtils.SplitDirAndFile(j.Dist.Tarball, out var tgzName);
                    _logger.Info($"Downloading Tarball {tgzName.ToString()}");
                    task = npmr.GetPackageTgz("typescript", tgzName.ToString());
                });
            }
            catch (Exception)
            {
                _logger.Error("Failed to parse TypeScript package info: " +
                              packageEtagAndContent.content.Substring(0,
                                  Math.Min(1000, packageEtagAndContent.content.Length)));
                throw;
            }

            if (task != null)
            {
                var bytes = await task;
                _logger.Info($"Extracting {bytes.Length} bytes");
                await TarExtractor.ExtractTgzAsync(bytes, async (name, stream, size) =>
                {
                    if (name.StartsWith("package/"))
                        name = name.Substring("package/".Length);
                    var fn = PathUtils.Join(dir, name);
                    Directory.CreateDirectory(PathUtils.DirToCreateDirectory(PathUtils.Parent(fn)));
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

        public void SetJasmineVersion(string version)
        {
            _jasmineVersion = version ?? "330";
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

        public byte[] WebGet(string path)
        {
            if (_proxyWeb != null)
            {
                try
                {
                    return ProxyGet(_proxyWeb, path);
                }
                catch (Exception e)
                {
                    _logger.Error("Proxy failure " + _proxyWeb + " " + path + " " + e.Message);
                }
            }

            WebZip.TryGetValue(path, out var result);
            return result;
        }

        public byte[] WebtGet(string path)
        {
            if (_proxyWebt != null)
            {
                try
                {
                    return ProxyGet(_proxyWebt, path);
                }
                catch (Exception e)
                {
                    _logger.Error("Proxy failure " + _proxyWebt + " " + path + " " + e.Message);
                }
            }

            WebtZip.TryGetValue(path, out var result);
            return result;
        }

        HttpClient _httpClient = new HttpClient();
        string _jasmineVersion;

        byte[] ProxyGet(string baseUrl, string path)
        {
            return _httpClient.GetByteArrayAsync(new Uri(baseUrl + path)).Result;
        }

        public void ProxyWeb(string url)
        {
            _proxyWeb = url;
        }

        public void ProxyWebt(string url)
        {
            _proxyWebt = url;
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
