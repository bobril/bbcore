using System;
using System.Text;
using Lib.DiskCache;
using Lib.ToolsDir;
using Lib.Utils;
using JavaScriptEngineSwitcher.Core;
using System.Linq;
using System.Diagnostics;
using Newtonsoft.Json;

namespace Lib.TSCompiler
{
    public class TsCompiler : ITSCompiler
    {
        public TsCompiler(IToolsDir toolsDir)
        {
            _toolsDir = toolsDir;
            _callbacks = new BBCallbacks(this);
        }

        IDiskCache _diskCache;
        readonly IToolsDir _toolsDir;

        public IDiskCache DiskCache { get => _diskCache; set => _diskCache = value; }
        public bool MeasurePerformance { get; set; }

        public ITSCompilerOptions CompilerOptions
        {
            get
            {
                var engine = getJSEnviroment();
                return JsonConvert.DeserializeObject<TSCompilerOptions>(engine.CallFunction<string>("bbGetCurrentCompilerOptions"));
            }
            set
            {
                var engine = getJSEnviroment();
                engine.CallFunction("bbSetCurrentCompilerOptions", JsonConvert.SerializeObject(value, Formatting.None, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));
            }
        }

        public ITSCompilerCtx Ctx { get; set; }

        public void MergeCompilerOptions(ITSCompilerOptions compilerOptions)
        {
            var engine = getJSEnviroment();
            engine.CallFunction("bbMergeCurrentCompilerOptions", JsonConvert.SerializeObject(compilerOptions, Formatting.None, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));
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
                var file = _owner._diskCache.TryGetItem(fullPath) as IFileCache;
                if (file == null)
                {
                    return null;
                }
                GetFileInfo(file).StartCompiling();
                return file.ChangeId;
            }

            public string readFile(string fileName, bool sourceCode)
            {
                var fullPath = PathUtils.Join(_owner._currentDirectory, fileName);
                var file = _owner._diskCache.TryGetItem(fullPath) as IFileCache;
                if (file == null)
                {
                    return null;
                }
                return file.Utf8Content;
            }

            public bool writeFile(string fileName, string data)
            {
                _owner.Ctx.writeFile(fileName, data);
                return true;
            }

            public bool dirExists(string directoryPath)
            {
                var fullPath = PathUtils.Join(_owner._currentDirectory, directoryPath);
                if (!fullPath.StartsWith(_owner._currentDirectory))
                    return false;
                return _owner._diskCache.TryGetItem(fullPath) is IDirectoryCache;
            }

            public bool fileExists(string fileName)
            {
                var fullPath = PathUtils.Join(_owner._currentDirectory, fileName);
                if (!fullPath.StartsWith(_owner._currentDirectory))
                    return false;
                return _owner._diskCache.TryGetItem(fullPath) is IFileCache;
            }

            public string getDirectories(string directoryPath)
            {
                var fullPath = PathUtils.Join(_owner._currentDirectory, directoryPath);
                if (!fullPath.StartsWith(_owner._currentDirectory))
                    return "";
                var dc = _owner._diskCache.TryGetItem(fullPath) as IDirectoryCache;
                if (dc == null)
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
                if (_owner.MeasurePerformance)
                    Trace.WriteLine((isError ? "Error:" : "Warn:") + code + " " + text);
            }

            public void reportTypeScriptDiagFile(bool isError, int code, string text, string fileName, int startLine, int startCharacter, int endLine, int endCharacter)
            {
                if (isError) _owner._wasError = true;
                _owner.Ctx.reportDiag(isError, code, text, fileName, startLine, startCharacter, endLine, endCharacter);
                if (_owner.MeasurePerformance)
                    Trace.WriteLine((isError ? "Error:" : "Warn:") + code + " " + text + " " + fileName + ":" + startLine);
            }

            TSProject GetDirectoryInfo(IDirectoryCache dir)
            {
                return TSProject.Get(dir, _owner.DiskCache);
            }

            TSFileAdditionalInfo GetFileInfo(IFileCache file)
            {
                return TSFileAdditionalInfo.Get(file, _owner.DiskCache);
            }

            TSFileAdditionalInfo GetFileInfo(string name)
            {
                var fullPath = PathUtils.Join(_owner._currentDirectory, name);
                var file = _owner._diskCache.TryGetItem(fullPath) as IFileCache;
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
                        _owner.Ctx.reportDiag(false, -2, "Fixing local import with two slashes: " + name, additionalInfo.Owner.FullPath, 0, 0, 0, 0);
                        name = name.Replace("//", "/");
                    }
                    var fullName = PathUtils.Join(PathUtils.Parent(containingFile), name);
                    res = _owner.Ctx.resolveLocalImport(fullName, additionalInfo);
                }
                else
                {
                    if (name.Contains('/') || name.Contains('\\'))
                    {
                        _owner.Ctx.reportDiag(true, -1, "Absolute import '" + name + "' must be just simple module name", containingFile, 0, 0, 0, 0);
                        return "";
                    }
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
                throw new ArgumentException("Unknown extension " + res + " in " + containingFile + " importing " + name);
            }

            public string resolvePathStringLiteral(string sourcePath, string text)
            {
                if (text.StartsWith("node_modules/"))
                {
                    return PathUtils.Join(_owner._currentDirectory, text);
                }
                return PathUtils.Join(PathUtils.Parent(sourcePath), text);
            }

            public void reportSourceInfo(string fileName, string info)
            {
                var fullPath = PathUtils.Join(_owner._currentDirectory, fileName);
                var file = _owner._diskCache.TryGetItem(fullPath) as IFileCache;
                if (file == null)
                {
                    return;
                }
                var fileInfo = GetFileInfo(file);
                fileInfo.SourceInfo = JsonConvert.DeserializeObject<SourceInfo>(info);
                _owner.Ctx.AddDependenciesFromSourceInfo(fileInfo);
            }

            public string getModifications(string fileName)
            {
                return JsonConvert.SerializeObject(_owner.Ctx.getPreEmitTransformations(fileName));
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
        string _commonSourceDirectory;
        private TimeSpan _gatherTime;

        public bool CompileProgram()
        {
            var engine = getJSEnviroment();
            _commonSourceDirectory = (string)engine.CallFunction("bbCompileProgram");
            if (_commonSourceDirectory.EndsWith("/"))
                _commonSourceDirectory = _commonSourceDirectory.Substring(0, _commonSourceDirectory.Length - 1);
            return !_wasError;
        }

        public string CommonSourceDirectory { get => _commonSourceDirectory; }

        public void GatherSourceInfo()
        {
            var engine = getJSEnviroment();
            var start = DateTime.UtcNow;
            engine.CallFunction("bbGatherSourceInfo");
            _gatherTime = DateTime.UtcNow - start;
        }

        public bool EmitProgram()
        {
            var engine = getJSEnviroment();
            var res = engine.CallFunction<bool>("bbEmitProgram") && !_wasError;
            if (MeasurePerformance)
            {
                Console.WriteLine(engine.CallFunction("bbFinishTSPerformance")+$" GatherInfo: {_gatherTime.TotalMilliseconds:0}");
            }
            return res;
        }
    }
}
