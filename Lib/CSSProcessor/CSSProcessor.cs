using System;
using Lib.ToolsDir;
using JavaScriptEngineSwitcher.Core;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Njsast.Css;

namespace Lib.CSSProcessor;

public class CssProcessor : ICssProcessor
{
    public CssProcessor(IToolsDir toolsDir)
    {
        _toolsDir = toolsDir;
        _callbacks = new BBCSSCallbacks(this);
    }

    readonly IToolsDir _toolsDir;
    Func<string, string, string> _urlReplacer;
    TaskCompletionSource<string> _tcs;
    public bool ForceNativeCss { get; set; }

    public class BBCSSCallbacks
    {
        CssProcessor _owner;

        public BBCSSCallbacks(CssProcessor owner)
        {
            _owner = owner;
        }

        public string urlReplace(string url, string from)
        {
            return _owner._urlReplacer(url, from);
        }

        public void finish(string result)
        {
            _owner._tcs.SetResult(result);
        }

        public void fail(string result)
        {
            _owner._tcs.SetException(new Exception(result));
        }

        public string readFileSync(string fileName)
        {
            return "";
        }
    }

    BBCSSCallbacks _callbacks;

    IJsEngine? _engine;

    IJsEngine getJSEnviroment()
    {
        if (_engine != null) return _engine;
        var engine = _toolsDir.CreateJsEngine();
        engine.EmbedHostObject("bb", _callbacks);
        var assembly = typeof(CssProcessor).Assembly;
        var assemblyName = assembly.GetName().Name!;
        engine.ExecuteResource($"{assemblyName}.CSSProcessor.bundle.min.js", assembly);
        engine.ExecuteResource($"{assemblyName}.CSSProcessor.bbcss.js", assembly);
        _engine = engine;
        return engine;
    }

    public Task<string> ProcessCss(string source, string from, Func<string, string, string> urlReplacer,
        Func<string, string, SourceFromPair?>? importLoader = null)
    {
        if (UseNativeCss)
            return Task.FromResult(ProcessCssNative(source, from, urlReplacer, importLoader, minify: false));
        _urlReplacer = urlReplacer;
        _tcs = new TaskCompletionSource<string>();
        var engine = getJSEnviroment();
        engine.CallFunction("bbProcessCss", source, from);
        return _tcs.Task;
    }

    public Task<string> ConcatenateAndMinifyCss(System.Collections.Generic.IEnumerable<SourceFromPair> inputs,
        Func<string, string, string> urlReplacer, Func<string, string, SourceFromPair?>? importLoader = null)
    {
        if (UseNativeCss)
            return Task.FromResult(ConcatenateAndMinifyCssNative(inputs, urlReplacer, importLoader));
        _urlReplacer = urlReplacer;
        _tcs = new TaskCompletionSource<string>();
        var engine = getJSEnviroment();
        var serializerSettings = new JsonSerializerSettings();
        serializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
        engine.CallFunction("bbConcatAndMinify", JsonConvert.SerializeObject(inputs, serializerSettings));
        return _tcs.Task;
    }

    static bool NativeCssEnabled =>
        string.Equals(Environment.GetEnvironmentVariable("BBCSS"), "native", StringComparison.OrdinalIgnoreCase);

    bool UseNativeCss => ForceNativeCss || NativeCssEnabled;

    static string ProcessCssNative(string source, string from, Func<string, string, string> urlReplacer,
        Func<string, string, SourceFromPair?>? importLoader, bool minify)
    {
        var stylesheet = CssParser.Parse(source, new CssParserOptions { SourceFile = from });
        InlineImports(stylesheet, from, importLoader, new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal));
        CssUrlRewriter.Rewrite(stylesheet, urlReplacer);
        return stylesheet.PrintToString(new CssOutputOptions
        {
            Beautify = !minify,
            PreserveComments = !minify
        });
    }

    static string ConcatenateAndMinifyCssNative(System.Collections.Generic.IEnumerable<SourceFromPair> inputs,
        Func<string, string, string> urlReplacer, Func<string, string, SourceFromPair?>? importLoader)
    {
        var stylesheets = new System.Collections.Generic.List<CssStylesheet>();
        foreach (var input in inputs)
        {
            var stylesheet = CssParser.Parse(input.Source, new CssParserOptions { SourceFile = input.From });
            InlineImports(stylesheet, input.From, importLoader, new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal));
            CssUrlRewriter.Rewrite(stylesheet, urlReplacer);
            stylesheets.Add(stylesheet);
        }

        var concatenated = CssStylesheet.Concat(stylesheets);
        CssMinifier.Minify(concatenated);
        return concatenated.PrintToString(new CssOutputOptions
        {
            Beautify = false,
            PreserveComments = false
        });
    }

    static void InlineImports(CssStylesheet stylesheet, string from,
        Func<string, string, SourceFromPair?>? importLoader, System.Collections.Generic.HashSet<string> stack)
    {
        if (importLoader == null) return;
        InlineImports(stylesheet.Nodes, from, importLoader, stack);
    }

    static void InlineImports(System.Collections.Generic.List<CssNode> nodes, string from,
        Func<string, string, SourceFromPair?> importLoader, System.Collections.Generic.HashSet<string> stack)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is CssAtRule { Nodes: null } atRule &&
                string.Equals(atRule.Name, "import", StringComparison.OrdinalIgnoreCase) &&
                TryGetImportUrlToInline(atRule.Parameters, out var importUrl))
            {
                var importedPair = importLoader(importUrl, from);
                if (importedPair == null) continue;
                var imported = importedPair.Value;
                if (!stack.Add(imported.From))
                    throw new InvalidOperationException("Circular CSS import " + imported.From);

                var importedStylesheet = CssParser.Parse(imported.Source, new CssParserOptions { SourceFile = imported.From });
                InlineImports(importedStylesheet, imported.From, importLoader, stack);
                stack.Remove(imported.From);
                nodes.RemoveAt(i);
                nodes.InsertRange(i, importedStylesheet.Nodes);
                i += importedStylesheet.Nodes.Count - 1;
                continue;
            }

            if (nodes[i] is CssRule rule)
            {
                InlineImports(rule.Nodes, rule.Source ?? from, importLoader, stack);
            }
            else if (nodes[i] is CssAtRule { Nodes: { } childNodes } childAtRule)
            {
                InlineImports(childNodes, childAtRule.Source ?? from, importLoader, stack);
            }
        }
    }

    static bool TryGetImportUrlToInline(string parameters, out string importUrl)
    {
        importUrl = "";
        var url = ExtractImportUrl(parameters);
        if (url == null || url.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("//", StringComparison.Ordinal) ||
            !url.StartsWith("file://", StringComparison.OrdinalIgnoreCase) && !IsRelativeUrl(url))
        {
            return false;
        }

        importUrl = url;
        return true;
    }

    static bool IsRelativeUrl(string url)
    {
        if (url.Length == 0) return false;
        if (url[0] is '/' or '\\') return false;
        return url.Length < 2 || url[1] != ':';
    }

    static string? ExtractImportUrl(string parameters)
    {
        var trimmed = parameters.TrimStart();
        if (trimmed.StartsWith("url(", StringComparison.OrdinalIgnoreCase))
        {
            var end = FindClosingParen(trimmed, 4);
            if (end < 0) return null;
            return Unquote(trimmed[4..end].Trim());
        }

        if (trimmed.Length == 0 || trimmed[0] is not ('"' or '\''))
            return null;

        var quote = trimmed[0];
        for (var i = 1; i < trimmed.Length; i++)
        {
            if (trimmed[i] == '\\')
            {
                i++;
                continue;
            }

            if (trimmed[i] == quote)
                return trimmed[1..i];
        }

        return null;
    }

    static int FindClosingParen(string text, int start)
    {
        var quote = '\0';
        for (var i = start; i < text.Length; i++)
        {
            var ch = text[i];
            if (quote != '\0')
            {
                if (ch == '\\') i++;
                else if (ch == quote) quote = '\0';
                continue;
            }

            if (ch is '"' or '\'') quote = ch;
            else if (ch == ')') return i;
        }

        return -1;
    }

    static string Unquote(string value)
    {
        return value.Length >= 2 && value[0] is '"' or '\'' && value[^1] == value[0] ? value[1..^1] : value;
    }

    public void Dispose()
    {
        _engine?.Dispose();
    }
}
