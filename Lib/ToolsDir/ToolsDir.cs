using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using JavaScriptEngineSwitcher.Core;
using Lib.DiskCache;
using Lib.Registry;
using Lib.Utils;
using Lib.Utils.Logger;
using Newtonsoft.Json.Linq;

namespace Lib.ToolsDir;

public class ToolsDir : IToolsDir
{
    static readonly object lockInitialization = new();
    private readonly IFsAbstraction _fsAbstraction;
    static readonly object _lock = new();

    public ToolsDir(string dir, IFsAbstraction fsAbstraction)
    {
        _fsAbstraction = fsAbstraction;
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
                }
            }
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
            var stringBuilder = new StringBuilder();
            if (_typeScriptJsContent == null)
            {
                var tmp = PathUtils.Join(TypeScriptLibDir, "typescript.js");
                stringBuilder.Append(Encoding.UTF8.GetString(_fsAbstraction.ReadAllBytes(tmp)));

                // Revert https://github.com/microsoft/TypeScript/pull/44624 - it makes output longer without real need
                // Also it prevents detection of bobril-g11n t calls.
                stringBuilder.Replace(
                    "if (indirectCall) {", "if (false) {"
                );

                // Patch strange crash in TS (4.2.4 probably more) in isFileLevelUniqueName function
                stringBuilder.Replace(
                    "return !(hasGlobalName && hasGlobalName(name)) && !sourceFile.identifiers.has(name);",
                    "return !(hasGlobalName && hasGlobalName(name)) && (!sourceFile?.identifiers?.has(name) ?? true);");

                // Patch strange crash in TS (4.1.3 probably more) https://github.com/microsoft/TypeScript/issues/40747
                stringBuilder.Replace(
                    "ts.Debug.assert(declarationTransform.transformed.length === 1, \"Should only see one output from the decl transform\");",
                    "ts.Debug.assert(declarationTransform.transformed.length === 1, \"Should only see one output from the decl transform\");declarationTransform.transformed[0].text = declarationTransform.transformed[0].text || \"\";");

                // Need always use __createBinding helper (allows mocking of exports) even though using ES5 target replace:
                // if (languageVersion === ScriptTarget.ES3) {
                // by
                // if (true) {
                stringBuilder.Replace("if (languageVersion === 0 /* ES3 */) {", "if (true) {");
                // In TypeScript 4.7 it has new form.
                stringBuilder.Replace("if (languageVersion === 0 /* ScriptTarget.ES3 */) {", "if (true) {");

                // Patch TS 3.9 and 4.0 to fix https://github.com/microsoft/TypeScript/issues/38691
                stringBuilder.Replace(
                    "ts.append(statements, factory.createExpressionStatement(ts.reduceLeft(currentModuleInfo.exportedNames, function (prev, nextId) { return factory.createAssignment(factory.createPropertyAccessExpression(factory.createIdentifier(\"exports\"), factory.createIdentifier(ts.idText(nextId))), prev); }, factory.createVoidZero())));",
                    " var chunkSize = 50;\n for (var i = 0; i < currentModuleInfo.exportedNames.length; i += chunkSize) {\n ts.append(statements, factory.createExpressionStatement(ts.reduceLeft(currentModuleInfo.exportedNames.slice(i, i + chunkSize), function (prev, nextId) { return factory.createAssignment(factory.createPropertyAccessExpression(factory.createIdentifier(\"exports\"), factory.createIdentifier(ts.idText(nextId))), prev); }, factory.createVoidZero())));\n}");

                // Patch TS to generate d.ts also from node_modules directory (it makes much faster typecheck when changing something in node_modules)
                // function isSourceFileFromExternalLibrary must return always false!
                stringBuilder.Replace("return !!sourceFilesFoundSearchingNodeModules.get(file.path);",
                        "return false;");
                // Patch TypeScript compiler to never generate useless __esmodule = true
                stringBuilder.Replace("(shouldEmitUnderscoreUnderscoreESModule())", "(false)");

                // Patch
                var bugPos =
                    stringBuilder.IndexOf("function checkUnusedClassMembers(", startIndex: 0);
                var bugPos22 = bugPos < 0
                    ? -1
                    : stringBuilder.IndexOf("case 158 /* IndexSignature */:", bugPos);
                var bugPos33 = bugPos22 < 0
                    ? -1
                    : stringBuilder.IndexOf("case 207", bugPos22);
                if (bugPos22 > 0 && (bugPos33 < 0 || bugPos33 > bugPos22 + 200))
                {
                    stringBuilder.Insert(bugPos22, "case 207:");
                }

                // Remove too defensive check for TS2742 - it is ok in Bobril-build to have relative paths into node_modules when in sandboxes
                stringBuilder.Replace(".indexOf(\"/node_modules/\") >= 0", "===null");
            }

            return stringBuilder.ToString();
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
