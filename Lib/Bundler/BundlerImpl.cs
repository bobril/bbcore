using JavaScriptEngineSwitcher.Core;
using Lib.ToolsDir;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;

namespace Lib.Bundler
{
    /// <summary>
    /// Old Bundler implemented in JavaScript - uses UglifyJs for compression and minification
    /// </summary>
    public class BundlerImpl : IBundler
    {
        readonly IToolsDir _toolsDir;
        readonly BBCallbacks _bbCallbacks;

        public IReadOnlyList<string>? MainFiles { get; set; }
        public bool Compress { get; set; }
        public bool Mangle { get; set; }
        public bool Beautify { get; set; }
        public IReadOnlyDictionary<string, object> Defines { get; set; }
        public IBundlerCallback? Callbacks { get; set; }

        public BundlerImpl(IToolsDir toolsDir)
        {
            _toolsDir = toolsDir;
            Compress = true;
            Mangle = true;
            Beautify = false;
            Defines = new Dictionary<string, object>();
            _bbCallbacks = new BBCallbacks(this);
        }

        public class BBCallbacks
        {
            readonly BundlerImpl _owner;

            public BBCallbacks(BundlerImpl owner)
            {
                _owner = owner;
            }

#pragma warning disable IDE1006 // Naming Styles
            public string readContent(string name) => _owner.Callbacks.ReadContent(name);
            public string getPlainJsDependencies(string name) => string.Join('|', _owner.Callbacks.GetPlainJsDependencies(name));
            public void writeBundle(string name, string content) => _owner.Callbacks.WriteBundle(name, content);
            public string generateBundleName(string forName) => _owner.Callbacks.GenerateBundleName(forName);
            public string resolveRequire(string name, string from) => _owner.Callbacks.ResolveRequire(name, from);
            public string tslibSource(bool withImport) => _owner.Callbacks.TslibSource(withImport);
            public void log(string text) => Console.WriteLine(text);
#pragma warning restore IDE1006 // Naming Styles
        }

        IJsEngine _engine;

        IJsEngine GetJSEnviroment()
        {
            if (_engine != null)
                return _engine;
            var engine = _toolsDir.CreateJsEngine();
            var assembly = typeof(BundlerImpl).Assembly;
            engine.ExecuteResource("Lib.Bundler.Js.uglify.js", assembly);
            engine.EmbedHostObject("bb", _bbCallbacks);
            engine.ExecuteResource("Lib.Bundler.Js.bundler.js", assembly);
            _engine = engine;
            return engine;
        }

        public void Bundle()
        {
            var engine = GetJSEnviroment();
            engine.CallFunction("bbBundle", JsonConvert.SerializeObject(new Dictionary<string, object> {
                { "mainFiles", MainFiles },
                { "compress", Compress },
                { "mangle", Mangle },
                { "beautify", Beautify },
                { "defines", Defines }
            }, Formatting.None, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, ContractResolver = new DefaultContractResolver { NamingStrategy = new DefaultNamingStrategy() } }));
        }
    }
}
