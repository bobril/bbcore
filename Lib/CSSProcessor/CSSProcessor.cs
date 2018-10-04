using System;
using Lib.ToolsDir;
using JavaScriptEngineSwitcher.Core;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Lib.CSSProcessor
{
    public class CssProcessor : ICssProcessor
    {
        public CssProcessor(IToolsDir toolsDir)
        {
            _toolsDir = toolsDir;
            _callbacks = new BBCallbacks(this);
        }

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

            public string readFileSync(string fileName)
            {
                return "";
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

        public Task<string> ProcessCss(string source, string from, Func<string, string, string> urlReplacer)
        {
            _urlReplacer = urlReplacer;
            _tcs = new TaskCompletionSource<string>();
            var engine = getJSEnviroment();
            engine.CallFunction("bbProcessCss", source, from);
            return _tcs.Task;
        }

        public Task<string> ConcatenateAndMinifyCss(System.Collections.Generic.IEnumerable<SourceFromPair> inputs, Func<string, string, string> urlReplacer)
        {
            _urlReplacer = urlReplacer;
            _tcs = new TaskCompletionSource<string>();
            var engine = getJSEnviroment();
            var serializerSettings = new JsonSerializerSettings();
            serializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            engine.CallFunction("bbConcatAndMinify", JsonConvert.SerializeObject(inputs, serializerSettings));
            return _tcs.Task;
        }

        public void Dispose()
        {
            _engine?.Dispose();
        }
    }
}
