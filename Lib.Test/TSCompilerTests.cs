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
using System.Runtime.InteropServices;
using Lib.BuildCache;

namespace Lib.Test
{
    public class FakeFsAbstraction : IFsAbstraction, IDirectoryWatcher
    {
        public bool IsMac => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

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

        Dictionary<KeyValuePair<string, string>, FakeFile> _content =
            new Dictionary<KeyValuePair<string, string>, FakeFile>();

        public IReadOnlyList<FsItemInfo> GetDirectoryContent(string path)
        {
            var res = new List<FsItemInfo>();
            foreach (var kv in _content)
            {
                if (kv.Key.Key != path) continue;
                if (kv.Value == null)
                    res.Add(FsItemInfo.Directory(kv.Key.Value, false));
                else
                    res.Add(FsItemInfo.Existing(kv.Key.Value, kv.Value._length, kv.Value._lastWriteTimeUtc));
            }

            return res;
        }

        public FsItemInfo GetItemInfo(ReadOnlySpan<char> path)
        {
            var d = PathUtils.SplitDirAndFile(path, out var ff).ToString();
            var f = ff.ToString();
            if (_content.TryGetValue(new KeyValuePair<string, string>(d, f), out var file))
            {
                if (file == null)
                    return FsItemInfo.Directory(d, false);
                return FsItemInfo.Existing(d, file._length, file._lastWriteTimeUtc);
            }

            return FsItemInfo.Missing();
        }

        public byte[] ReadAllBytes(string path)
        {
            var d = PathUtils.SplitDirAndFile(path, out var ff).ToString();
            var f = ff.ToString();
            if (_content.TryGetValue(new KeyValuePair<string, string>(d, f), out var file))
            {
                if (file == null)
                    throw new Exception("Cannot read directory as file " + path);
                return Encoding.UTF8.GetBytes(file._content);
            }

            throw new Exception("Not found file " + path);
        }

        public string ReadAllUtf8(string path)
        {
            var d = PathUtils.SplitDirAndFile(path, out var ff).ToString();
            var f = ff.ToString();
            if (_content.TryGetValue(new KeyValuePair<string, string>(d, f), out var file))
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
            var d = PathUtils.SplitDirAndFile(path, out var ff).ToString();
            var f = ff.ToString();
            if (_content.TryGetValue(new KeyValuePair<string, string>(d, f), out var file))
            {
                if (file == null)
                    throw new Exception("Cannot add file because it is already dir " + path);
                file._lastWriteTimeUtc = DateTime.UtcNow;
                file._content = content;
                file._length = (ulong) Encoding.UTF8.GetByteCount(content);
                return;
            }

            CreateDir(d);
            _content[new KeyValuePair<string, string>(d, f)] = new FakeFile
            {
                _content = content,
                _length = (ulong) Encoding.UTF8.GetByteCount(content),
                _lastWriteTimeUtc = DateTime.UtcNow
            };
            OnFileChange?.Invoke(path);
        }

        void CreateDir(string path)
        {
            var d = PathUtils.SplitDirAndFile(path, out var ff).ToString();
            var f = ff.ToString();
            if (_content.TryGetValue(new KeyValuePair<string, string>(d, f), out var file))
            {
                if (file != null)
                {
                    throw new Exception("mkdir fail already file " + path);
                }

                return;
            }

            if (d != "")
            {
                CreateDir(d);
            }

            _content[new KeyValuePair<string, string>(d, f)] = null;
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

        public bool FileExists(string path)
        {
            return true;
        }
    }

    [CollectionDefinition("Serial", DisableParallelization = true)]
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
            _tools = new ToolsDir.ToolsDir(PathUtils.Join(_bbdir, "tools"), new DummyLogger());
            _tools.SetTypeScriptVersion(ProjectOptions.DefaultTypeScriptVersion);
            _compilerPool = new CompilerPool(_tools, new DummyLogger());
        }

        [Fact]
        public void DefaultTypeScriptVersionDidntChanged()
        {
            Assert.Equal("3.7.4", _tools.TypeScriptVersion);
        }

        [Fact]
        public void TranspilerWorks()
        {
            var ts = _compilerPool.GetTs(null, new TSCompilerOptions { newLine = NewLineKind.LineFeed });
            var res = ts.Transpile("index.ts", "let a: string = 'ahoj';");
            Assert.Equal("var a = 'ahoj';\n", res.JavaScript);
            _compilerPool.ReleaseTs(ts);
        }
    }
}
