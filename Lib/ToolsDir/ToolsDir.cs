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
        static object _lock = new object();

        public ToolsDir(string dir)
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

        public string WebtAJs { get; }

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
            RunYarn(Path, "add typescript@" + version + " --no-emoji --non-interactive");
        }

        public void RunYarn(string dir, string aParams)
        {
            var yarnPath = Environment.GetEnvironmentVariable("PATH").Split(System.IO.Path.PathSeparator).Select((p) => PathUtils.Join(PathUtils.Normalize(new DirectoryInfo(p).FullName), "yarn.cmd")).First((p) => File.Exists(p));
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

        public IJsEngine CreateJsEngine()
        {
            var jsEngineSwitcher = JsEngineSwitcher.Current;
            return jsEngineSwitcher.CreateDefaultEngine();
        }
    }
}
