using Lib.DiskCache;
using Lib.Utils;
using System;
using Xunit;
using System.Collections.Generic;
using Lib.Watcher;
using System.Text;
using System.Linq;
using Lib.TSCompiler;
using Lib.Composition;

namespace Lib.Test
{
    public class FakeFsAbstraction : IFsAbstraction, IDirectoryWatcher
    {
        public bool IsUnixFs => PathUtils.IsUnixFs;

        public string WatchedDirectory { get; set; }
        public Action<string> OnFileChange { get; set; }
        public Action OnError { get; set; }

        public class FakeFile
        {
            public string _content;
            public ulong _length;
            public DateTime _lastWriteTimeUtc;
        }

        Dictionary<KeyValuePair<string, string>, FakeFile> _content = new Dictionary<KeyValuePair<string, string>, FakeFile>();

        public IReadOnlyList<FsItemInfo> GetDirectoryContent(string path)
        {
            var res = new List<FsItemInfo>();
            foreach (var kv in _content)
            {
                if (kv.Key.Key != path) continue;
                if (kv.Value == null)
                    res.Add(FsItemInfo.Directory(kv.Key.Value));
                else
                    res.Add(FsItemInfo.Existing(kv.Key.Value, kv.Value._length, kv.Value._lastWriteTimeUtc));
            }
            return res;
        }

        public FsItemInfo GetItemInfo(string path)
        {
            var fad = PathUtils.SplitDirAndFile(path);
            if (_content.TryGetValue(new KeyValuePair<string, string>(fad.Item1, fad.Item2), out var file))
            {
                if (file == null)
                    return FsItemInfo.Directory(fad.Item2);
                return FsItemInfo.Existing(fad.Item2, file._length, file._lastWriteTimeUtc);
            }
            return FsItemInfo.Missing();
        }

        public byte[] ReadAllBytes(string path)
        {
            var fad = PathUtils.SplitDirAndFile(path);
            if (_content.TryGetValue(new KeyValuePair<string, string>(fad.Item1, fad.Item2), out var file))
            {
                if (file == null)
                    throw new Exception("Cannot read directory as file " + path);
                return Encoding.UTF8.GetBytes(file._content);
            }
            throw new Exception("Not found file " + path);
        }

        public string ReadAllUtf8(string path)
        {
            var fad = PathUtils.SplitDirAndFile(path);
            if (_content.TryGetValue(new KeyValuePair<string, string>(fad.Item1, fad.Item2), out var file))
            {
                if (file == null)
                    throw new Exception("Cannot read directory as file " + path);
                return file._content;
            }
            throw new Exception("Not found file " + path);
        }

        public void Dispose()
        {
        }

        public void AddTextFile(string path, string content)
        {
            var fad = PathUtils.SplitDirAndFile(path);
            if (_content.TryGetValue(new KeyValuePair<string, string>(fad.Item1, fad.Item2), out var file))
            {
                if (file == null)
                    throw new Exception("Cannot add file because it is already dir " + path);
                file._lastWriteTimeUtc = DateTime.UtcNow;
                file._content = content;
                file._length = (ulong)Encoding.UTF8.GetByteCount(content);
                OnFileChange?.Invoke(path);
                return;
            }
            CreateDir(fad.Item1);
            _content[new KeyValuePair<string, string>(fad.Item1, fad.Item2)] = new FakeFile
            {
                _content = content,
                _length = (ulong)Encoding.UTF8.GetByteCount(content),
                _lastWriteTimeUtc = DateTime.UtcNow
            };
            OnFileChange?.Invoke(path);
        }

        private void CreateDir(string path)
        {
            var fad = PathUtils.SplitDirAndFile(path);
            if (fad.Item1 == null) fad.Item1 = "";
            if (_content.TryGetValue(new KeyValuePair<string, string>(fad.Item1, fad.Item2), out var file))
            {
                if (file != null)
                {
                    throw new Exception("mkdir fail already file " + path);
                }
                return;
            }
            if (fad.Item1 != "")
            {
                CreateDir(fad.Item1);
            }
            _content[new KeyValuePair<string, string>(fad.Item1, fad.Item2)] = null;
        }

        public void AddNativeDir(string path)
        {
            var nfs = new NativeFsAbstraction();
            foreach (var f in nfs.GetDirectoryContent(path))
            {
                if (f.IsDirectory) continue;
                AddTextFile(PathUtils.Join(path, f.Name), nfs.ReadAllUtf8(PathUtils.Join(path, f.Name)));
            }
        }
    }

    public class CompilerTests
    {
        string _bbdir;
        ToolsDir.ToolsDir _tools;
        CompilerPool _compilerPool;
        FakeFsAbstraction fs;
        string projdir;
        DiskCache.DiskCache dc;

        public CompilerTests()
        {
            _bbdir = PathUtils.Join(PathUtils.Normalize(Environment.CurrentDirectory), ".bbcore");
            _tools = new ToolsDir.ToolsDir(PathUtils.Join(_bbdir, "tools"));
            _tools.InstallTypeScriptVersion();
            _compilerPool = new CompilerPool(_tools);
        }

        [Fact]
        public void LatestTypeScriptVersionDidntChanged()
        {
            Assert.Equal("2.5.3", _tools.GetTypeScriptVersion());
        }

        [Fact]
        public void SimpliestProjectCompiles()
        {
            InitFakeProject();
            AddSimpleProjectJson();
            fs.AddTextFile(PathUtils.Join(projdir, "index.ts"), @"
console.log(""Hello"");
            ");
            BuildResult buildResult = BuildProject();
            Assert.Single(buildResult.RecompiledLast);
            Assert.Single(buildResult.Path2FileInfo);
            Assert.Contains("Hello", buildResult.RecompiledLast.First().Output);
        }

        [Fact]
        public void ChangeOfSimpleIndexTsRebuildsIt()
        {
            SimpliestProjectCompiles();
            fs.AddTextFile(PathUtils.Join(projdir, "index.ts"), @"
console.log(""Changed"");
            ");
            BuildResult buildResult = BuildProject();
            Assert.Single(buildResult.RecompiledLast);
            Assert.Single(buildResult.Path2FileInfo);
            Assert.Contains("Changed", buildResult.RecompiledLast.First().Output);
        }

        [Fact]
        public void LocalDependencyCompiles()
        {
            InitFakeProject();
            AddSimpleProjectJson();
            fs.AddTextFile(PathUtils.Join(projdir, "index.ts"), @"
import * as lib from ""./lib"";
console.log(""Hello "" + lib.fn());
            ");
            fs.AddTextFile(PathUtils.Join(projdir, "lib.ts"), @"
export function fn() { return ""Lib42""; }
            ");
            BuildResult buildResult = BuildProject();
            Assert.Equal(2, buildResult.RecompiledLast.Count);
            Assert.Equal(2, buildResult.Path2FileInfo.Count);
        }

        [Fact]
        public void ImplChangingLocalLibRecompilesJustOneFile()
        {
            LocalDependencyCompiles();
            fs.AddTextFile(PathUtils.Join(projdir, "lib.ts"), @"
export function fn() { return ""Lib42!""; }
            ");
            BuildResult buildResult = BuildProject();
            Assert.Single(buildResult.RecompiledLast);
            Assert.Equal(2, buildResult.Path2FileInfo.Count);
        }

        [Fact]
        public void IfaceChangingLocalLibRecompilesBothFiles()
        {
            LocalDependencyCompiles();
            fs.AddTextFile(PathUtils.Join(projdir, "lib.ts"), @"
export function fn() { return ""Lib42!""; }
export var change = true;
            ");
            BuildResult buildResult = BuildProject();
            Assert.Equal(2, buildResult.RecompiledLast.Count);
            Assert.Equal(2, buildResult.Path2FileInfo.Count);
        }

        [Fact]
        public void StyleDefAddNamesWithoutPrefixing()
        {
            InitFakeProject();
            AddSimpleProjectJson();
            AddBobrilWithStyleDef();
            fs.AddTextFile(PathUtils.Join(projdir, "index.ts"), @"
import * as b from ""bobril"";
var s1 = b.styleDef({ color: ""blue"" });
var s2 = b.styleDef({ color: ""red"" }, { hover: { color: ""navy"" } });
var s3 = b.styleDef({}, undefined, ""myname"");
var s4 = b.styleDefEx(s1, {});
var s5 = b.styleDefEx(s2, {}, {});
var s6 = b.styleDefEx([s1, s2], {}, {}, ""advname""); 
            ");
            BuildResult buildResult = BuildProject();
            Assert.Equal(@"""use strict"";
var b = require(""bobril"");
var s1 = b.styleDef({ color: ""blue"" }, void 0, ""s1"");
var s2 = b.styleDef({ color: ""red"" }, { hover: { color: ""navy"" } }, ""s2"");
var s3 = b.styleDef({}, undefined, ""myname"");
var s4 = b.styleDefEx(s1, {}, void 0, ""s4"");
var s5 = b.styleDefEx(s2, {}, {}, ""s5"");
var s6 = b.styleDefEx([s1, s2], {}, {}, ""advname"");".Replace("\r",""), buildResult.Path2FileInfo[PathUtils.Join(projdir, "index.ts")].Output);
        }

        void AddSimpleProjectJson()
        {
            fs.AddTextFile(PathUtils.Join(projdir, "package.json"), @"
{
    ""name"": ""a""
}
            ");
        }

        BuildResult BuildProject(Action<ProjectOptions> configure = null)
        {
            var ctx = new BuildCtx(_compilerPool);
            var dirCache = dc.TryGetItem(projdir) as IDirectoryCache;
            var proj = TSProject.Get(dirCache, dc);
            proj.IsRootProject = true;
            if (proj.ProjectOptions == null)
            {
                proj.ProjectOptions = new ProjectOptions
                {
                    Tools = _tools,
                    Owner = proj,
                    Defines = new Dictionary<string, bool> { { "DEBUG", true } }
                };
                proj.LoadProjectJson();
                proj.ProjectOptions.RefreshMainFile();
                proj.ProjectOptions.RefreshTestSources();
                proj.ProjectOptions.DetectBobrilJsxDts();
                proj.ProjectOptions.RefreshExampleSources();
            }
            configure?.Invoke(proj.ProjectOptions);
            ctx.TSCompilerOptions = new TSCompilerOptions
            {
                sourceMap = true,
                skipLibCheck = true,
                skipDefaultLibCheck = true,
                target = ScriptTarget.ES5,
                preserveConstEnums = false,
                jsx = JsxEmit.React,
                reactNamespace = "b",
                experimentalDecorators = true,
                noEmitHelpers = true,
                allowJs = true,
                checkJs = false,
                removeComments = false,
                types = new string[0],
                lib = new HashSet<string> { "es5", "dom", "es2015.core", "es2015.promise", "es2015.iterable", "es2015.collection" }
            };
            ctx.Sources = new HashSet<string>();
            ctx.Sources.Add(proj.MainFile);
            proj.ProjectOptions.ExampleSources.ForEach(s => ctx.Sources.Add(s));
            if (proj.ProjectOptions.BobrilJsxDts != null)
                ctx.Sources.Add(proj.ProjectOptions.BobrilJsxDts);
            proj.Build(ctx);
            return ctx.BuildResult;
        }

        void InitFakeProject()
        {
            fs = new FakeFsAbstraction();
            projdir = PathUtils.Join(PathUtils.Normalize(Environment.CurrentDirectory), "proj");
            fs.AddNativeDir(_tools.TypeScriptLibDir);
            dc = new DiskCache.DiskCache(fs, () => fs);
            dc.AddRoot(_tools.TypeScriptLibDir);
            dc.AddRoot(projdir);
        }

        void AddBobrilWithStyleDef()
        {
            fs.AddTextFile(PathUtils.Join(projdir, "node_modules/bobril/package.json"), @"
{
    ""name"": ""bobril"",
    ""main"": ""index.js""
}
            ");
            fs.AddTextFile(PathUtils.Join(projdir, "node_modules/bobril/index.ts"), @"
export type IBobrilStyleDef = string;
export function styleDef(
  style: any,
  pseudo?: { [name: string]: any },
  nameHint?: string
): IBobrilStyleDef {
  return """";
}
export function styleDefEx(
  parent: IBobrilStyleDef | IBobrilStyleDef[] | undefined,
  style: any,
  pseudo?: { [name: string]: any },
  nameHint?: string
): IBobrilStyleDef {
  return """";
}
            ");
        }
    }
}
