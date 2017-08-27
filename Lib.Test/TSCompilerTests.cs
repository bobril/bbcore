using Lib.DiskCache;
using Lib.Utils;
using System;
using Xunit;
using System.Collections.Generic;
using Lib.Watcher;
using System.Text;
using System.Linq;
using Lib.TSCompiler;
using Lib.ToolsDir;

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
                if (file!=null)
                {
                    throw new Exception("mkdir fail already file " + path);
                }
                return;
            }
            if (fad.Item1!="")
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
        private TSCompilerPool _compilerPool;
        private FakeFsAbstraction fs;
        private string projdir;
        private DiskCache.DiskCache dc;

        public CompilerTests()
        {
            _bbdir = PathUtils.Join(PathUtils.Normalize(Environment.CurrentDirectory), ".bbcore");
            _tools = new ToolsDir.ToolsDir(PathUtils.Join(_bbdir, "tools"));
            _tools.InstallTypeScriptVersion();
            _compilerPool = new TSCompilerPool(_tools);
        }

        [Fact]
        public void LatestTypeScriptVersionDidntChanged()
        {
            Assert.Equal("2.4.2", _tools.GetTypeScriptVersion());
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
            Assert.Equal(1, buildResult.RecompiledLast.Count);
            Assert.Equal(1, buildResult.WithoutExtension2Source.Count);
            Assert.Contains("Hello", buildResult.RecompiledLast.First().JsLink.Owner.Utf8Content);
        }

        [Fact]
        public void ChangeOfSimpleIndexTsRebuildsIt()
        {
            SimpliestProjectCompiles();
            fs.AddTextFile(PathUtils.Join(projdir, "index.ts"), @"
console.log(""Changed"");
            ");
            BuildResult buildResult = BuildProject();
            Assert.Equal(1, buildResult.RecompiledLast.Count);
            Assert.Equal(1, buildResult.WithoutExtension2Source.Count);
            Assert.Contains("Changed", buildResult.RecompiledLast.First().JsLink.Owner.Utf8Content);
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
            Assert.Equal(2, buildResult.WithoutExtension2Source.Count);
        }

        [Fact]
        public void ImplChangingLocalLibRecompilesJustOneFile()
        {
            LocalDependencyCompiles();
            fs.AddTextFile(PathUtils.Join(projdir, "lib.ts"), @"
export function fn() { return ""Lib42!""; }
            ");
            BuildResult buildResult = BuildProject();
            Assert.Equal(1, buildResult.RecompiledLast.Count);
            Assert.Equal(2, buildResult.WithoutExtension2Source.Count);
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
            Assert.Equal(2, buildResult.WithoutExtension2Source.Count);
        }

        void AddSimpleProjectJson()
        {
            fs.AddTextFile(PathUtils.Join(projdir, "package.json"), @"
{
    ""name"": ""a""
}
            ");
        }

        BuildResult BuildProject()
        {
            var ctx = new BuildCtx(_compilerPool);
            var dirCache = dc.TryGetItem(projdir) as IDirectoryCache;
            var proj = TSProject.Get(dirCache, dc);
            proj.Build(ctx);
            var buildResult = proj.BuildResult;
            return buildResult;
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
    }
}
