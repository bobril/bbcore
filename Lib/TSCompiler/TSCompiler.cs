using System;
using System.Text;
using Lib.DiskCache;
using Lib.ToolsDir;
using Lib.Utils;
using JavaScriptEngineSwitcher.Core;
using System.Diagnostics;
using Newtonsoft.Json;
using Lib.Utils.Logger;
using System.Collections.Generic;

namespace Lib.TSCompiler
{
    public class TsCompiler : ITSCompiler
    {
        public TsCompiler(IToolsDir toolsDir, ILogger logger)
        {
            Logger = logger;
            _toolsDir = toolsDir;
            _callbacks = new BBCallbacks(this);
        }

        readonly IToolsDir _toolsDir;

        public ILogger Logger { get; }
        public IDiskCache DiskCache { get; set; }
        public bool MeasurePerformance { get; set; }

        TranspileResult _transpileResult;
        ITSCompilerOptions _lastCompilerOptions;

        public ITSCompilerOptions CompilerOptions
        {
            get
            {
                var engine = getJSEnviroment();
                return JsonConvert.DeserializeObject<TSCompilerOptions>(engine.CallFunction<string>("bbGetCurrentCompilerOptions"));
            }
            set
            {
                if (_lastCompilerOptions == value) return;
                _lastCompilerOptions = value;
                var engine = getJSEnviroment();
                engine.CallFunction("bbSetCurrentCompilerOptions", JsonConvert.SerializeObject(value, Formatting.None, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));
            }
        }

        public ITSCompilerCtx Ctx { get; set; }

        class BBCallbacks
        {
            TsCompiler _owner;

            public BBCallbacks(TsCompiler owner)
            {
                _owner = owner;
            }

            public int? getChangeId(string fileName)
            {
                var fullPath = PathUtils.Join(_owner._currentDirectory, fileName);
                var file = _owner.Ctx.TryGetFile(fullPath);
                if (file == null)
                {
                    return null;
                }
                return file.ChangeId;
            }

#pragma warning disable IDE0060 // Remove unused parameter
            public string readFile(string fileName, bool sourceCode)
#pragma warning restore IDE0060 // Remove unused parameter
            {
                var fullPath = PathUtils.Join(_owner._currentDirectory, fileName);
                return _owner.Ctx.readFile(fullPath);
            }

            public bool dirExists(string directoryPath)
            {
                var fullPath = PathUtils.Join(_owner._currentDirectory, directoryPath);
                var dir = _owner.DiskCache.TryGetItem(fullPath) as IDirectoryCache;
                return dir != null && !dir.IsInvalid;
            }

            public bool fileExists(string fileName)
            {
                var fullPath = PathUtils.Join(_owner._currentDirectory, fileName);
                var file = _owner.Ctx.TryGetFile(fullPath);
                return file != null && !file.IsInvalid;
            }

            public string getDirectories(string directoryPath)
            {
                var fullPath = PathUtils.Join(_owner._currentDirectory, directoryPath);
                var dc = _owner.DiskCache.TryGetItem(fullPath) as IDirectoryCache;
                if (dc == null || dc.IsInvalid)
                    return "";
                var sb = new StringBuilder();
                foreach (var item in dc)
                {
                    if (item is IDirectoryCache)
                    {
                        if (sb.Length > 0) sb.Append('|');
                        sb.Append(item.Name);
                    }
                }
                return sb.ToString();
            }

            public string realPath(string path)
            {
                //Console.WriteLine("realPath:" + path);
                return PathUtils.Normalize(path);
            }

            public void trace(string text)
            {
                Console.WriteLine("TSCompiler trace:" + text);
            }

            public void reportTypeScriptDiag(bool isError, int code, string text)
            {
                if (isError) _owner._wasError = true;
                var tr = _owner._transpileResult;
                if (tr != null)
                {
                    if (tr.Diagnostics == null) tr.Diagnostics = new List<Diagnostic>();
                    tr.Diagnostics.Add(new Diagnostic
                    {
                        IsError = isError,
                        Code = code,
                        Text = text
                    });
                }
                if (_owner.MeasurePerformance)
                    Trace.WriteLine((isError ? "Error:" : "Warn:") + code + " " + text);
            }

            public void reportTypeScriptDiagFile(bool isError, int code, string text, string fileName, int startLine, int startCharacter, int endLine, int endCharacter)
            {
                if (isError) _owner._wasError = true;
                var tr = _owner._transpileResult;
                if (tr != null)
                {
                    if (tr.Diagnostics == null) tr.Diagnostics = new List<Diagnostic>();
                    tr.Diagnostics.Add(new Diagnostic
                    {
                        IsError = isError,
                        Code = code,
                        Text = text,
                        FileName = fileName,
                        StartLine = startLine,
                        StartCol = startCharacter,
                        EndLine = endLine,
                        EndCol = endCharacter
                    });
                }
                else
                {
                    _owner.Ctx.reportDiag(isError, code, text, fileName, startLine, startCharacter, endLine, endCharacter);
                }
                if (_owner.MeasurePerformance)
                    Trace.WriteLine((isError ? "Error:" : "Warn:") + code + " " + text + " " + fileName + ":" + startLine);
            }

            TSFileAdditionalInfo GetFileInfo(IFileCache file)
            {
                return TSFileAdditionalInfo.Create(file, _owner.DiskCache);
            }

            TSFileAdditionalInfo GetFileInfo(string name)
            {
                var fullPath = PathUtils.Join(_owner._currentDirectory, name);
                var file = _owner.Ctx.TryGetFile(fullPath);
                return GetFileInfo(file);
            }

            // resolvedName:string|isExternalLibraryImport:boolean|extension:string(Ts,Tsx,Dts,Js,Jsx)
            public string resolveModuleName(string name, string containingFile)
            {
                var additionalInfo = GetFileInfo(containingFile);
                string res;
                if (name.Length > 1 && name[0] == '.')
                {
                    if (name.Contains("//"))
                    {
                        _owner.Ctx.reportDiag(false, -11, "Fixing local import with two slashes: " + name, additionalInfo.Owner.FullPath, 0, 0, 0, 0);
                        name = name.Replace("//", "/");
                    }
                    var fullName = PathUtils.Join(PathUtils.Parent(containingFile), name);
                    res = _owner.Ctx.resolveLocalImport(fullName, additionalInfo);
                }
                else
                {
                    res = _owner.Ctx.resolveModuleMain(name, additionalInfo);
                }
                if (res == null) return "";
                if (res.EndsWith(".d.ts"))
                    return res + "|true|Dts";
                if (res.EndsWith(".ts"))
                    return res + "|false|Ts";
                if (res.EndsWith(".tsx"))
                    return res + "|false|Tsx";
                if (res.EndsWith(".js"))
                    return res + "|false|Js";
                if (res.EndsWith(".jsx"))
                    return res + "|false|Jsx";
                if (res.EndsWith(".json"))
                    return res + "|false|Json";
                throw new ArgumentException("Unknown extension " + res + " in " + containingFile + " importing " + name);
            }

            public string resolvePathStringLiteral(string sourcePath, string text)
            {
                if (text.StartsWith("resource:"))
                {
                    return "resource:" + resolvePathStringLiteral(sourcePath, text.Substring(9));
                }
                if (text.StartsWith("node_modules/"))
                {
                    var dir = _owner._currentDirectory.AsSpan();
                    while (dir != null)
                    {
                        var fp = PathUtils.Join(dir, text);
                        if (!(_owner.DiskCache.TryGetItem(fp)?.IsInvalid ?? true)) return fp;
                        dir = PathUtils.Parent(dir);
                    }
                    return text;
                }
                return PathUtils.Join(PathUtils.Parent(sourcePath), text);
            }
        }

        BBCallbacks _callbacks;

        IJsEngine _engine;

        IJsEngine getJSEnviroment()
        {
            if (_engine != null) return _engine;
            var engine = _toolsDir.CreateJsEngine();
            engine.Execute(_toolsDir.TypeScriptJsContent, _toolsDir.TypeScriptLibDir + "/typescript.js");
            engine.EmbedHostObject("bb", _callbacks);
            var assembly = typeof(TsCompiler).Assembly;
            engine.ExecuteResource("Lib.TSCompiler.bbtsc.js", assembly);
            engine.SetVariableValue("bbDefaultLibLocation", _toolsDir.TypeScriptLibDir);
            _engine = engine;
            return engine;
        }

        public TranspileResult Transpile(string fileName, string content)
        {
            var engine = getJSEnviroment();
            _transpileResult = new TranspileResult();
            try
            {
                _transpileResult.JavaScript = engine.CallFunction("bbTranspile", fileName, content) as string;
                _transpileResult.SourceMap = engine.CallFunction("bbGetLastSourceMap") as string;
                return _transpileResult;
            }
            finally
            {
                _transpileResult = null;
            }
        }

        public void CreateProgram(string currentDirectory, string[] mainFiles)
        {
            _wasError = false;
            var engine = getJSEnviroment();
            _currentDirectory = currentDirectory;
            if (MeasurePerformance)
            {
                engine.CallFunction("bbStartTSPerformance");
            }
            engine.SetVariableValue("bbCurrentDirectory", currentDirectory);
            engine.CallFunction("bbCreateProgram", string.Join('|', mainFiles));
        }

        bool _wasError;
        string _currentDirectory;

        public bool CompileProgram()
        {
            var engine = getJSEnviroment();
            CommonSourceDirectory = (string)engine.CallFunction("bbCompileProgram");
            if (CommonSourceDirectory.EndsWith("/"))
                CommonSourceDirectory = CommonSourceDirectory.Substring(0, CommonSourceDirectory.Length - 1);
            return !_wasError;
        }

        public string CommonSourceDirectory { get; private set; }

        public void Dispose()
        {
            _engine?.Dispose();
        }

        public string GetTSVersion()
        {
            var engine = getJSEnviroment();
            return engine.Evaluate<string>("ts.version");
        }
    }
}
