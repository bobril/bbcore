using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using JavaScriptEngineSwitcher.Core;
using Lib.Utils;
using Newtonsoft.Json.Linq;

namespace Lib.ToolsDir
{
    public class ToolsDir : IToolsDir
    {
        static object lockInitialization = new object();

        const string YarnExecutableName = "yarn";
        static object _lock = new object();

        public ToolsDir(string dir)
        {
            lock (lockInitialization)
            {
                Path = dir;
                if (!new DirectoryInfo(dir).Exists)
                    Directory.CreateDirectory(Path);
                TypeScriptLibDir = PathUtils.Join(Path, "node_modules/typescript/lib");
                lock (_lock)
                {
                    var jsEngineSwitcher = JsEngineSwitcher.Current;
                    if (!jsEngineSwitcher.EngineFactories.Any())
                    {
                        jsEngineSwitcher.EngineFactories.Add(new JavaScriptEngineSwitcher.ChakraCore.ChakraCoreJsEngineFactory());
                        jsEngineSwitcher.DefaultEngineName = JavaScriptEngineSwitcher.ChakraCore.ChakraCoreJsEngine.EngineName;
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
            }
        }

        public string Path { get; }
        public string TypeScriptLibDir { get; }

        string _typeScriptJsContent;

        public string TypeScriptJsContent
        {
            get
            {
                if (_typeScriptJsContent == null)
                {
                    _typeScriptJsContent = File.ReadAllText(PathUtils.Join(TypeScriptLibDir, "typescript.js"));

                    // Patch TypeScript compiler to never generate useless __esmodule = true
                    _typeScriptJsContent = _typeScriptJsContent.Replace("(shouldEmitUnderscoreUnderscoreESModule())", "(false)");
                    // Patch https://github.com/Microsoft/TypeScript/commit/c557131cac4379fc3e685514d44b6b82f1f642fb
                    var bugPos = _typeScriptJsContent.IndexOf("// As the type information we would attempt to lookup to perform ellision is potentially unavailable for the synthesized nodes");
                    if (bugPos > 0)
                    {
                        var bugPos3 = _typeScriptJsContent.IndexOf("visitEachChild", bugPos);
                        var bugPos2 = _typeScriptJsContent.IndexOf("return node;", bugPos);
                        // but only when it is already not fixed
                        if (bugPos3 < 0 || bugPos3 > bugPos2)
                        {
                            _typeScriptJsContent = _typeScriptJsContent.Insert(bugPos2, "if (node.transformFlags & 2) { return ts.visitEachChild(node, visitor, context); };");
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

        private readonly JObject _localeDefs;

        public string WebtAJs { get; }

        public string TsLibSource { get; }

        public string ImportSource { get; }

        public string GetTypeScriptVersion()
        {
            var tspackage = PathUtils.Join(Path, "node_modules/typescript/package.json");
            if (!File.Exists(tspackage))
                return null;
            try
            {
                var package = JObject.Parse(File.ReadAllText(tspackage));
                var version = package.Property("version").Value.ToString();
                return version;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return null;
            }
        }

        public void InstallTypeScriptVersion(string version = "*")
        {
            _typeScriptJsContent = null;
            var tspackage = PathUtils.Join(Path, "package.json");
            if (!File.Exists(tspackage))
                RunYarn(Path, "init -y");
            RunYarn(Path, "add typescript@" + version + " --no-emoji --non-interactive");
            if (version=="*") RunYarn(Path, "upgrade --no-emoji --non-interactive");
        }

        public void RunYarn(string dir, string aParams)
        {            
            var yarnPath = GetYarnPath();
            var start = new ProcessStartInfo(yarnPath, aParams)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                WorkingDirectory = dir,
                StandardOutputEncoding = Encoding.UTF8
            };
            var process = Process.Start(start);
            process.OutputDataReceived += (object sendingProcess, DataReceivedEventArgs outLine) =>
            {
                Console.WriteLine(outLine.Data);
            };
            process.BeginOutputReadLine();
            process.WaitForExit();
        }

        string GetYarnPath() {
            string yarnExecName = YarnExecutableName;
            if (!PathUtils.IsUnixFs) {
                yarnExecName += ".cmd";                    
            }
            return Environment.GetEnvironmentVariable("PATH").Split(System.IO.Path.PathSeparator).Select((p) => PathUtils.Join(PathUtils.Normalize(new DirectoryInfo(p).FullName), yarnExecName)).First((p) => File.Exists(p));
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
                if (dashIndex < 0) return null;
                locale = locale.Substring(0, dashIndex);
            }
        }
    }
}
