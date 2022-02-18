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

namespace Lib.ToolsDir;

public class ToolsDir : IToolsDir
{
    readonly ILogger _logger;
    static readonly object lockInitialization = new();

    static readonly object _lock = new();

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
                        new JavaScriptEngineSwitcher.V8.V8JsEngineFactory(
                            new() { DisableGlobalMembers = false }));
                    jsEngineSwitcher.DefaultEngineName =
                        JavaScriptEngineSwitcher.V8.V8JsEngine.EngineName;
                    /*jsEngineSwitcher.EngineFactories.Add(
                        new JavaScriptEngineSwitcher.ChakraCore.ChakraCoreJsEngineFactory(
                            new JavaScriptEngineSwitcher.ChakraCore.ChakraCoreSettings {MaxStackSize = 2000000, DisableFatalOnOOM = true}));
                    jsEngineSwitcher.DefaultEngineName =
                        JavaScriptEngineSwitcher.ChakraCore.ChakraCoreJsEngine.EngineName;*/
                }
            }

            SetJasmineVersion("3.3");
            LoaderJs = ResourceUtils.GetText("Lib.ToolsDir.loader.js").Replace("\"use strict\";", "");
            JasmineCoreJs299 = ResourceUtils.GetText("Lib.ToolsDir.jasmine299.js");
            JasmineDts299 = ResourceUtils.GetText("Lib.ToolsDir.jasmine299.d.ts");
            JasmineDtsPath299 = PathUtils.Join(Path, "jasmine.d.ts");
            if (!File.Exists(JasmineDtsPath299) || File.ReadAllText(JasmineDtsPath299) != JasmineDts299)
                File.WriteAllText(JasmineDtsPath299, JasmineDts299);
            JasmineBootJs299 = ResourceUtils.GetText("Lib.ToolsDir.jasmine-boot299.js");
            JasmineBootJs330 = ResourceUtils.GetText("Lib.ToolsDir.jasmine-boot330.js");
            JasmineBootJs400 = ResourceUtils.GetText("Lib.ToolsDir.jasmine-boot400.js");
            JasmineCoreJs330 = ResourceUtils.GetText("Lib.ToolsDir.jasmine330.js");
            JasmineCoreJs400 = ResourceUtils.GetText("Lib.ToolsDir.jasmine400.js");
            JasmineDts330 = ResourceUtils.GetText("Lib.ToolsDir.jasmine330.d.ts");
            JasmineDts400 = ResourceUtils.GetText("Lib.ToolsDir.jasmine400.d.ts");
            JasmineDtsPath330 = PathUtils.Join(Path, "jasmine330.d.ts");
            JasmineDtsPath400 = PathUtils.Join(Path, "jasmine400.d.ts");
            if (!File.Exists(JasmineDtsPath330) || File.ReadAllText(JasmineDtsPath330) != JasmineDts330)
                File.WriteAllText(JasmineDtsPath330, JasmineDts330);
            if (!File.Exists(JasmineDtsPath400) || File.ReadAllText(JasmineDtsPath400) != JasmineDts400)
                File.WriteAllText(JasmineDtsPath400, JasmineDts400);

            WebtZip = ResourceUtils.GetZip("Lib.ToolsDir.webt.zip");
            WebZip = ResourceUtils.GetZip("Lib.ToolsDir.web.zip");
            CoverageDetailsVisualizerZip = ResourceUtils.GetZip("Lib.ToolsDir.CoverageDetailsVisualizer.zip");

            _localeDefs = JObject.Parse(ResourceUtils.GetText("Lib.ToolsDir.localeDefs.json"));
            LiveReloadJs = ResourceUtils.GetText("Lib.ToolsDir.liveReload.js");
        }
    }

    public IDictionary<string, byte[]> WebZip { get; }

    public IDictionary<string, byte[]> WebtZip { get; }

    public IDictionary<string, byte[]> CoverageDetailsVisualizerZip { get; }

    public string Path { get; }
    public string TypeScriptLibDir { get; private set; }
    public string TypeScriptVersion { get; private set; }

    string? _typeScriptJsContent;

    public string TypeScriptJsContent
    {
        get
        {
            if (_typeScriptJsContent == null)
            {
                _typeScriptJsContent = File.ReadAllText(PathUtils.Join(TypeScriptLibDir, "typescript.js"));

                // Revert https://github.com/microsoft/TypeScript/pull/44624 - it makes output longer without real need
                // Also it prevents detection of bobril-g11n t calls.
                _typeScriptJsContent = _typeScriptJsContent.Replace(
                    "if (indirectCall) {", "if (false) {"
                );

                // Patch strange crash in TS (4.2.4 probably more) in isFileLevelUniqueName function
                _typeScriptJsContent = _typeScriptJsContent.Replace(
                    "return !(hasGlobalName && hasGlobalName(name)) && !sourceFile.identifiers.has(name);",
                    "return !(hasGlobalName && hasGlobalName(name)) && (!sourceFile?.identifiers?.has(name) ?? true);");

                // Patch strange crash in TS (4.1.3 probably more) https://github.com/microsoft/TypeScript/issues/40747
                _typeScriptJsContent = _typeScriptJsContent.Replace(
                    "ts.Debug.assert(declarationTransform.transformed.length === 1, \"Should only see one output from the decl transform\");",
                    "ts.Debug.assert(declarationTransform.transformed.length === 1, \"Should only see one output from the decl transform\");declarationTransform.transformed[0].text = declarationTransform.transformed[0].text || \"\";");

                // Need always use __createBinding helper (allows mocking of exports) even though using ES5 target replace:
                // if (languageVersion === ScriptTarget.ES3) {
                // by
                // if (true) {
                _typeScriptJsContent =
                    _typeScriptJsContent.Replace("if (languageVersion === 0 /* ES3 */) {", "if (true) {");

                // Patch TS 3.9 and 4.0 to fix https://github.com/microsoft/TypeScript/issues/38691
                _typeScriptJsContent = _typeScriptJsContent.Replace(
                    "ts.append(statements, factory.createExpressionStatement(ts.reduceLeft(currentModuleInfo.exportedNames, function (prev, nextId) { return factory.createAssignment(factory.createPropertyAccessExpression(factory.createIdentifier(\"exports\"), factory.createIdentifier(ts.idText(nextId))), prev); }, factory.createVoidZero())));",
                    " var chunkSize = 50;\n for (var i = 0; i < currentModuleInfo.exportedNames.length; i += chunkSize) {\n ts.append(statements, factory.createExpressionStatement(ts.reduceLeft(currentModuleInfo.exportedNames.slice(i, i + chunkSize), function (prev, nextId) { return factory.createAssignment(factory.createPropertyAccessExpression(factory.createIdentifier(\"exports\"), factory.createIdentifier(ts.idText(nextId))), prev); }, factory.createVoidZero())));\n}");

                // Patch TS to generate d.ts also from node_modules directory (it makes much faster typecheck when changing something in node_modules)
                // function isSourceFileFromExternalLibrary must return always false!
                _typeScriptJsContent =
                    _typeScriptJsContent.Replace("return !!sourceFilesFoundSearchingNodeModules.get(file.path);",
                        "return false;");
                // Patch TypeScript compiler to never generate useless __esmodule = true
                _typeScriptJsContent =
                    _typeScriptJsContent.Replace("(shouldEmitUnderscoreUnderscoreESModule())", "(false)");

                // Patch
                var bugPos =
                    _typeScriptJsContent.IndexOf("function checkUnusedClassMembers(", StringComparison.Ordinal);
                var bugPos22 = bugPos < 0
                    ? -1
                    : _typeScriptJsContent.IndexOf("case 158 /* IndexSignature */:", bugPos,
                        StringComparison.Ordinal);
                var bugPos33 = bugPos22 < 0
                    ? -1
                    : _typeScriptJsContent.IndexOf("case 207", bugPos22, StringComparison.Ordinal);
                if (bugPos22 > 0 && (bugPos33 < 0 || bugPos33 > bugPos22 + 200))
                {
                    _typeScriptJsContent = _typeScriptJsContent.Insert(bugPos22, "case 207:");
                }

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

    public string JasmineCoreJs400 { get; }
    public string JasmineBootJs400 { get; }
    public string JasmineDts400 { get; }
    public string JasmineDtsPath400 { get; }

    public string JasmineCoreJs => _jasmineVersion switch
    {
        "2.99" => JasmineCoreJs299,
        "4.0" => JasmineCoreJs400,
        _ => JasmineCoreJs330
    };

    public string JasmineBootJs => _jasmineVersion switch
    {
        "2.99" => JasmineBootJs299,
        "4.0" => JasmineBootJs400,
        _ => JasmineBootJs330
    };

    public string JasmineDts => _jasmineVersion switch
    {
        "2.99" => JasmineDts299,
        "4.0" => JasmineDts400,
        _ => JasmineDts330
    };

    public string JasmineDtsPath => _jasmineVersion switch
    {
        "2.99" => JasmineDtsPath299,
        "4.0" => JasmineDtsPath400,
        _ => JasmineDtsPath330
    };

    readonly JObject _localeDefs;
    string _proxyWeb;
    string _proxyWebt;

    public string LiveReloadJs { get; }

    public async Task DownloadAndExtractTS(string dir, string versionString)
    {
        _logger.Info($"Downloading and extracting TypeScript {versionString}");
        var version = new SemanticVersioning.Version(versionString);
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
                await using var targetStream = File.Create(fn);
                var buf = new byte[4096];
                while (size > 0)
                {
                    var read = await stream.ReadAsync(buf.AsMemory(0, (int)Math.Min((ulong)buf.Length, size)));
                    if (read == 0) throw new IOException($"Reading {name} failed");
                    await targetStream.WriteAsync(buf.AsMemory(0, read));
                    size -= (ulong)read;
                }

                return true;
            });
        }
        else
        {
            throw new($"TypeScript version {version} does not exists");
        }
    }

    public void SetJasmineVersion(string? version)
    {
        _jasmineVersion = version ?? "3.3";
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

    HttpClient _httpClient = new();
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
