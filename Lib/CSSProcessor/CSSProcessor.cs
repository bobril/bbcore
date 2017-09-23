using System;
using Lib.DiskCache;
using Lib.ToolsDir;
using JavaScriptEngineSwitcher.Core;
using System.Threading.Tasks;

namespace Lib.CSSProcessor
{
    public class CssProcessor : ICssProcessor
    {
        public CssProcessor(IToolsDir toolsDir)
        {
            _toolsDir = toolsDir;
            _callbacks = new BBCallbacks(this);
        }

        IDiskCache _diskCache;
        readonly IToolsDir _toolsDir;
        Func<string, string, string> _urlReplacer;
        TaskCompletionSource<string> _tcs;

        class BBCallbacks
        {
            CssProcessor _owner;

            public BBCallbacks(CssProcessor owner)
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
        }

        BBCallbacks _callbacks;

        IJsEngine _engine;

        IJsEngine getJSEnviroment()
        {
            if (_engine != null) return _engine;
            var engine = _toolsDir.CreateJsEngine();
            engine.EmbedHostObject("bb", _callbacks);
            var assembly = typeof(CssProcessor).Assembly;
            engine.ExecuteResource("Lib.CSSProcessor.bundle.min.js", assembly);
            engine.ExecuteResource("Lib.CSSProcessor.bbcss.js", assembly);
            _engine = engine;
            return engine;
        }

        public Task<string> ProcessCss(string source, string from, Func<string, string, string> urlReplacerUrlFrom)
        {
            _urlReplacer = urlReplacerUrlFrom;
            _tcs = new TaskCompletionSource<string>();
            var engine = getJSEnviroment();
            engine.CallFunction("bbProcessCss", source, from);
            return _tcs.Task;
        }
    }
}
