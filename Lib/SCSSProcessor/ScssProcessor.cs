using System;
using System.Threading.Tasks;
using JavaScriptEngineSwitcher.Core;
using Lib.CSSProcessor;
using Lib.ToolsDir;
using Lib.Utils;

namespace Lib.SCSSProcessor;

public class ScssProcessor : IScssProcessor
{
    public ScssProcessor(IToolsDir toolsDir)
    {
        _toolsDir = toolsDir;
        _callbacks = new BBSCSSCallbacks(this);
    }

    readonly IToolsDir _toolsDir;
    Func<string, string> _loader;
    Action<string> _log;
    TaskCompletionSource<string> _tcs;

    public class BBSCSSCallbacks
    {
        ScssProcessor _owner;

        public BBSCSSCallbacks(ScssProcessor owner)
        {
            _owner = owner;
        }

        public void log(string text)
        {
            _owner._log(text);
        }

        public void finish(string result)
        {
            _owner._tcs.SetResult(result);
        }

        public void fail(string result)
        {
            _owner._tcs.SetException(new Exception(result));
        }

        public string load(string fileName)
        {
            return _owner._loader(fileName);
        }

        public string join(string p1, string p2)
        {
            if (p1.StartsWith("file://"))
            {
                p1 = p1[7..];
            }
            if (p2.StartsWith("file://"))
            {
                p2 = p2[7..];
            }

            return "file://" + PathUtils.Join(p1, p2);
        }
    }

    BBSCSSCallbacks _callbacks;

    IJsEngine? _engine;

    IJsEngine getJSEnviroment()
    {
        if (_engine != null) return _engine;
        var engine = _toolsDir.CreateJsEngine();
        engine.EmbedHostObject("bb", _callbacks);
        var assembly = typeof(CssProcessor).Assembly;
        engine.ExecuteResource("Lib.SCSSProcessor.bbscss.js", assembly);
        engine.ExecuteResource("Lib.SCSSProcessor.sass.dart.js", assembly);
        _engine = engine;
        engine.CallFunction("bbInit");
        return engine;
    }

    public void Dispose()
    {
        _engine?.Dispose();
    }

    public Task<string> ProcessScss(string source, string @from, Func<string, string> loader, Action<string> log)
    {
        _loader = loader;
        _log = log;
        _tcs = new();
        var engine = getJSEnviroment();
        engine.CallFunction("bbCompileScss", source, from);
        return _tcs.Task;
    }
}
