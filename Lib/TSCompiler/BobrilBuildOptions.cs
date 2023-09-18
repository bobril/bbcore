using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Lib.TSCompiler;

public class BobrilBuildOptions
{
    public string? tsVersion { get; set; }
    public string? variant { get; set; }
    public string? jasmineVersion { get; set; }
    public bool? nohtml { get; set; }
    public string? title { get; set; }
    public string? head { get; set; }
    public string? prefixStyleDefs { get; set; }
    public string? example { get; set; }
    public string? additionalResourcesDirectory { get; set; }
    public ITSCompilerOptions? compilerOptions { get; set; }
    public string? dependencies { get; set; }
    public IList<string>? includeSources { get; set; }
    public IList<int>? ignoreDiagnostic { get; set; }
    public bool? GenerateSpritesTs { get; set; } // plugins.bb-assets-generator-plugin.generateSpritesFile
    public bool? warningsAsErrors { get; set; }
    public string? obsolete { get; set; }
    public bool? interactiveDumpsToDist { get; set; }
    public IList<string>? testDirectories { get; set; }
    public bool? localize { get; set; }
    public string? pathToTranslations { get; set; }
    public bool? tsconfigUpdate { get; set; }
    public string? buildOutputDir { get; set; }
    public IDictionary<string, string>? defines { get; set; }
    public IDictionary<string, string>? envs { get; set; }
    public Dictionary<string, string?>? imports { get; set; }
    public bool? preserveProjectRoot { get; set; }
    public string? proxyUrl { get; set; }
    public string? headlessBrowserStrategy { get; set; }
    public bool? library { get; set; }
    public Dictionary<string, string>? assets { get; set; }

    public BobrilBuildOptions Merge(BobrilBuildOptions? with)
    {
        if (with == null) return this;
        if (with.tsVersion != null)
            tsVersion = with.tsVersion;
        if (with.variant != null)
            variant = with.variant;
        if (with.jasmineVersion != null)
            jasmineVersion = with.jasmineVersion;
        if (with.nohtml != null)
            nohtml = with.nohtml;
        if (with.title != null)
            title = with.title;
        if (with.head != null)
            head = with.head;
        if (with.prefixStyleDefs != null)
            prefixStyleDefs = with.prefixStyleDefs;
        if (with.example != null)
            example = with.example;
        if (with.additionalResourcesDirectory != null)
            additionalResourcesDirectory = with.additionalResourcesDirectory;
        if (with.compilerOptions != null)
            compilerOptions = compilerOptions != null ? compilerOptions.Merge(with.compilerOptions) : with.compilerOptions;
        if (with.dependencies != null)
            dependencies = with.dependencies;
        if (with.includeSources != null)
            includeSources = with.includeSources;
        if (with.ignoreDiagnostic != null)
            ignoreDiagnostic = with.ignoreDiagnostic;
        if (with.GenerateSpritesTs != null)
            GenerateSpritesTs = with.GenerateSpritesTs;
        if (with.warningsAsErrors != null)
            warningsAsErrors = with.warningsAsErrors;
        if (with.obsolete != null)
            obsolete = with.obsolete;
        if (with.interactiveDumpsToDist != null)
            interactiveDumpsToDist = with.interactiveDumpsToDist;
        if (with.testDirectories != null)
            testDirectories = with.testDirectories;
        if (with.localize != null)
            localize = with.localize;
        if (with.pathToTranslations != null)
            pathToTranslations = with.pathToTranslations;
        if (with.tsconfigUpdate != null)
            tsconfigUpdate = with.tsconfigUpdate;
        if (with.buildOutputDir != null)
            buildOutputDir = with.buildOutputDir;
        if (with.defines != null)
        {
            if (defines != null)
                foreach (var (k,v) in with.defines)
                {
                    defines[k] = v;
                }
            else
                defines = with.defines;
        }

        if (with.envs != null)
        {
            if (envs != null)
                foreach (var (k,v) in with.envs)
                {
                    envs[k] = v;
                }
            else
                envs = with.envs;
        }

        if (with.imports != null)
        {
            if (imports != null)
                foreach (var (k,v) in with.imports)
                {
                    imports[k] = v;
                }
            else
                imports = with.imports;
        }
        
        if (with.preserveProjectRoot != null)
            preserveProjectRoot = with.preserveProjectRoot;
        if (with.proxyUrl != null)
            proxyUrl = with.proxyUrl;
        if (with.headlessBrowserStrategy != null)
            headlessBrowserStrategy = with.headlessBrowserStrategy;
        if (with.library != null)
            library = with.library;
        if (with.assets != null)
            assets = with.assets;
        return this;
    }

    static string? GetStringProperty(JObject? obj, string name)
    {
        if (obj != null && obj.TryGetValue(name, out var value) && value.Type == JTokenType.String)
            return (string) value!;
        return null;
    }

    public BobrilBuildOptions(JToken? jToken)
    {
        if (jToken is not JObject bobrilSection) return;
        tsVersion = GetStringProperty(bobrilSection, "tsVersion");
        variant = GetStringProperty(bobrilSection, "variant");
        jasmineVersion = GetStringProperty(bobrilSection, "jasmineVersion");
        try
        {
            nohtml = bobrilSection["nohtml"]?.Value<bool>();
        }
        catch
        {
            // ignored
        }

        title = GetStringProperty(bobrilSection, "title");
        head = GetStringProperty(bobrilSection, "head");
        prefixStyleDefs = GetStringProperty(bobrilSection, "prefixStyleDefs");
        example = GetStringProperty(bobrilSection, "example");
        additionalResourcesDirectory =
            GetStringProperty(bobrilSection, "additionalResourcesDirectory");
        compilerOptions = TSCompilerOptions.Parse(bobrilSection!.GetValue("compilerOptions") as JObject);
        dependencies = GetStringProperty(bobrilSection, "dependencies");
        var includeSourcesJson = bobrilSection.GetValue("includeSources") as JArray;
        includeSources = includeSourcesJson?.Select(i => i.ToString()).ToArray();
        if (bobrilSection.GetValue("ignoreDiagnostic") is JArray ignoreDiagnosticJson)
            ignoreDiagnostic = ignoreDiagnosticJson.Select(i => i.Value<int>()).ToArray();
        var pluginsSection = bobrilSection.GetValue("plugins") as JObject;
        GenerateSpritesTs =
            pluginsSection?["bb-assets-generator-plugin"]?["generateSpritesFile"]?.Value<bool>();
        try
        {
            GenerateSpritesTs ??= bobrilSection["generateSpritesTs"]?.Value<bool>();
        }
        catch
        {
            // ignored
        }

        try
        {
            warningsAsErrors = bobrilSection["warningsAsErrors"]?.Value<bool>();
        }
        catch
        {
            // ignored
        }

        obsolete = GetStringProperty(bobrilSection, "obsolete");
        try
        {
            interactiveDumpsToDist = bobrilSection["interactiveDumpsToDist"]?.Value<bool>();
        }
        catch
        {
            interactiveDumpsToDist = true;
        }

        try
        {
            testDirectories = bobrilSection["testDirectories"]?.Values<string>().ToList();
        }
        catch
        {
            // ignored
        }

        try
        {
            localize = bobrilSection["localize"]?.Value<bool>();
        }
        catch
        {
            // ignored
        }
        pathToTranslations = GetStringProperty(bobrilSection, "pathToTranslations");

        try
        {
            tsconfigUpdate = bobrilSection["tsconfigUpdate"]?.Value<bool>();
        }
        catch
        {
            // ignored
        }

        buildOutputDir = GetStringProperty(bobrilSection, "buildOutputDir");
        defines = (bobrilSection.GetValue("defines") as JObject)?
            .Select<KeyValuePair<string, JToken?>, KeyValuePair<string, string>>(kv =>
                KeyValuePair.Create(kv.Key, kv.Value?.ToString() ?? ""))?
            .ToDictionary(kv=>kv.Key,kv=>kv.Value);
        envs = (bobrilSection.GetValue("envs") as JObject)?
            .Select<KeyValuePair<string, JToken?>, KeyValuePair<string, string>>(kv =>
                KeyValuePair.Create(kv.Key, kv.Value?.ToString() ?? ""))?
            .ToDictionary(kv=>kv.Key,kv=>kv.Value);
        try
        {
            preserveProjectRoot = bobrilSection["preserveProjectRoot"]?.Value<bool>();
        }
        catch
        {
            // ignored
        }

        proxyUrl = GetStringProperty(bobrilSection, "proxyUrl");
        headlessBrowserStrategy = GetStringProperty(bobrilSection, "headlessBrowserStrategy");
        try
        {
            library = bobrilSection["library"]?.Value<bool>();
        }
        catch
        {
            // ignored
        }

        if (bobrilSection.GetValue("assets") is JObject assetsJson)
        {
            foreach (var (key, value) in assetsJson)
            {
                if (value?.Type != JTokenType.String) continue;
                assets ??= new();
                assets.Add(key, value.Value<string>()!);
            }
        }
        if (bobrilSection.GetValue("imports") is JObject importsJson)
        {
            foreach (var (key, value) in importsJson)
            {
                if (value?.Type is not (JTokenType.String or JTokenType.Null)) continue;
                imports ??= new();
                imports.Add(key, value.Value<string>()!);
            }
        }
    }
}
