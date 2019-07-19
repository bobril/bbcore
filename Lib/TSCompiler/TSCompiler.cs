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
using Njsast;

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
                var file = _owner.DiskCache.TryGetItem(fullPath);
                if (file == null || file.IsInvalid)
                {
                    return null;
                }
                return file.ChangeId;
            }

            public string readFile(string fileName)
            {
                var fullPath = PathUtils.Join(_owner._currentDirectory, fileName);
                var file = _owner.DiskCache.TryGetItem(fullPath) as IFileCache;
                if (file != null && !file.IsInvalid)
                {
                    return file.Utf8Content;
                }
                return null;
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
                var file = _owner.DiskCache.TryGetItem(fullPath) as IFileCache;
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
                return path;
                //return PathUtils.RealPath(path);
            }

            public void trace(string text)
            {
                Console.WriteLine("TSCompiler trace:" + text);
            }

            public void reportTypeScriptDiag(bool isError, int code, string text)
            {
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
                else
                {
                    if (_owner.Logger.Verbose)
                    {
                        if (isError)
                        {
                            _owner.Logger.Error("TS" + code + ": " + text);
                        }
                        else
                        {
                            _owner.Logger.Info("TS" + code + ": " + text);
                        }
                    }
                }
            }

            public void reportTypeScriptDiagFile(bool isError, int code, string text, string fileName, int startLine, int startCharacter, int endLine, int endCharacter)
            {
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
                    _owner._diagnostics.Add(new Diagnostic
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
            }
        }

        BBCallbacks _callbacks;
        StructList<Diagnostic> _diagnostics = new StructList<Diagnostic>();

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

        public void ClearDiagnostics()
        {
            _diagnostics.Clear();
        }

        public Diagnostic[] GetDiagnostics()
        {
            var res = _diagnostics.ToArray();
            _diagnostics.ClearAndTruncate();
            return res;
        }

        public void CreateProgram(string currentDirectory, string[] mainFiles)
        {
            var engine = getJSEnviroment();
            _currentDirectory = currentDirectory;
            engine.SetVariableValue("bbCurrentDirectory", currentDirectory);
            engine.CallFunction("bbCreateWatchProgram", string.Join('|', mainFiles));
        }

        public void UpdateProgram(string[] mainFiles)
        {
            var engine = getJSEnviroment();
            engine.CallFunction("bbUpdateSourceList", string.Join('|', mainFiles));
        }
        string _currentDirectory;

        public void TriggerUpdate()
        {
            var engine = getJSEnviroment();
            engine.CallFunction("bbTriggerUpdate");
        }

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
