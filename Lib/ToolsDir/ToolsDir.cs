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
                    Directory.CreateDirectory(dir);
                lock (_lock)
                {
                    var jsEngineSwitcher = JsEngineSwitcher.Current;
                    if (!jsEngineSwitcher.EngineFactories.Any())
                    {
                        jsEngineSwitcher.EngineFactories.Add(new JavaScriptEngineSwitcher.ChakraCore.ChakraCoreJsEngineFactory(new JavaScriptEngineSwitcher.ChakraCore.ChakraCoreSettings { MaxStackSize = 2000000 }));
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
                LiveReloadJs = ResourceUtils.GetText("Lib.ToolsDir.liveReload.js");
            }
        }

        public string Path { get; }
        public string TypeScriptLibDir { get; private set; }
        public string TypeScriptVersion { get; private set; }

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
                    // Patch 
                    bugPos = _typeScriptJsContent.IndexOf("function checkUnusedClassMembers(");
                    var bugPos22 = bugPos < 0 ? -1 : _typeScriptJsContent.IndexOf("case 158 /* IndexSignature */:", bugPos);
                    var bugPos33 = bugPos22 < 0 ? -1 : _typeScriptJsContent.IndexOf("case 207", bugPos22);
                    if (bugPos22 > 0 && (bugPos33 < 0 || bugPos33 > bugPos22 + 200))
                    {
                        _typeScriptJsContent = _typeScriptJsContent.Insert(bugPos22, "case 207:");
                    }
                    // Patch https://github.com/Microsoft/TypeScript/issues/22403 in 2.8.1
                    bugPos = _typeScriptJsContent.IndexOf("if (links.target && links.target !== unknownSymbol && links.target !== resolvingSymbol) {");
                    if (bugPos > 0)
                    {
                        var bugPos2a = _typeScriptJsContent.IndexOf("links.nameType = getLiteralTypeFromPropertyName(links.target);", bugPos);
                        if (bugPos2a > 0 && bugPos2a - bugPos < 400)
                        {
                            _typeScriptJsContent = _typeScriptJsContent.Remove(bugPos, bugPos2a - bugPos);
                            _typeScriptJsContent = _typeScriptJsContent.Insert(bugPos, "if (links.target && links.target !== unknownSymbol && links.target !== resolvingSymbol && links.target.escapedName === prop.escapedName) {");
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

        public string LiveReloadJs { get; }

        public void SetTypeScriptVersion(string version)
        {
            if (TypeScriptVersion == version)
                return;
            _typeScriptJsContent = null;
            var tsVerDir = PathUtils.Join(Path, version);
            var tspackage = PathUtils.Join(tsVerDir, "package.json");
            lock (_lock)
            {
                if (!File.Exists(tspackage))
                {
                    Directory.CreateDirectory(tsVerDir);
                    RunYarn(tsVerDir, "init -y --no-emoji --non-interactive");
                    RunYarn(tsVerDir, "add typescript@" + version + " --no-emoji --non-interactive");
                }
            }
            TypeScriptLibDir = PathUtils.Join(tsVerDir, "node_modules/typescript/lib");
            TypeScriptVersion = version;
        }

        public void RunYarn(string dir, string aParams)
        {
            var yarnPath = GetYarnPath();
            var start = new ProcessStartInfo(yarnPath, aParams)
            {
                UseShellExecute = false,
                WorkingDirectory = dir,
                StandardErrorEncoding = Encoding.UTF8,
                StandardOutputEncoding = Encoding.UTF8,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true
            };

            var process = Process.Start(start);
            process.OutputDataReceived += Process_OutputDataReceived;
            process.ErrorDataReceived += Process_OutputDataReceived;
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();
            process.WaitForExit();
        }

        void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine(e.Data);
        }

        string GetYarnPath()
        {
            string yarnExecName = YarnExecutableName;
            if (!PathUtils.IsUnixFs)
            {
                yarnExecName += ".cmd";
            }
            return Environment.GetEnvironmentVariable("PATH")
                .Split(System.IO.Path.PathSeparator)
                .Where(t => !string.IsNullOrEmpty(t))
                .Select((p) => PathUtils.Join(PathUtils.Normalize(new DirectoryInfo(p).FullName), yarnExecName))
                .First((p) => File.Exists(p));
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
                if (dashIndex < 0)
                    return null;
                locale = locale.Substring(0, dashIndex);
            }
        }

        public void UpdateDependencies(string dir, bool upgrade, string npmRegistry)
        {
            if (npmRegistry != null)
            {
                if (!File.Exists(PathUtils.Join(dir, ".npmrc")))
                {
                    File.WriteAllText(PathUtils.Join(dir, ".npmrc"), "registry =" + npmRegistry);
                }
            }
            if (upgrade && !File.Exists(PathUtils.Join(dir, "yarn.lock")))
            {
                upgrade = false;
            }
            RunYarn(dir, (upgrade ? "upgrade" : "install") + " --flat --no-emoji --non-interactive");
        }
    }
}
