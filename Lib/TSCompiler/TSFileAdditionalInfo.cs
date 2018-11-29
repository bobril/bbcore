using Lib.BuildCache;
using Lib.DiskCache;
using Lib.Utils;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Collections.Generic;
using System.Linq;

namespace Lib.TSCompiler
{
    public enum FileCompilationType
    {
        Unknown,
        TypeScript,
        JavaScript,
        Css,
        Resource,
        JavaScriptAsset
    }

    public class TSFileAdditionalInfo
    {
        public FileCompilationType Type;
        public IFileCache Owner { get; set; }
        public IDiskCache DiskCache { get; set; }
        public List<int> LastCompilationCacheIds { get; set; }
        public TSFileAdditionalInfo DtsLink { get; set; }
        public string Output { get; set; }
        public SourceMap MapLink { get; set; }
        public SourceInfo SourceInfo { get; set; }
        public Image<Rgba32> Image { get; set; }
        public int ImageCacheId;
        public string OutputUrl { get; set; }
        public string FromModule;

        public bool TakenFromBuildCache;
        public byte[] BuildCacheHash;
        public uint BuildCacheConfId;
        public TSFileBuildCache BuildCacheValue;

        List<TSProject> _moduleImports;

        public IReadOnlyList<TSProject> ModuleImports
        {
            get => _moduleImports;
        }

        TSFileAdditionalInfo()
        {
        }

        public void StartCompiling()
        {
            _moduleImports = new List<TSProject>();
            _localImports = new List<TSFileAdditionalInfo>();
            _diag = null;
        }

        public void ImportingModule(TSProject name)
        {
            _moduleImports.Add(name);
        }

        List<TSFileAdditionalInfo> _localImports;
        List<Diag> _diag;
        public TSProject MyProject;

        public IReadOnlyList<Diag> Diagnostic => _diag;

        public IReadOnlyList<TSFileAdditionalInfo> LocalImports
        {
            get => _localImports;
        }

        public string ImportedAsModule { get; internal set; }

        public void ImportingLocal(TSFileAdditionalInfo name)
        {
            _localImports.Add(name);
        }

        public void ReportDiag(bool isError, int code, string text, int startLine, int startCharacter, int endLine,
            int endCharacter)
        {
            if (_diag == null) _diag = new List<Diag>();
            _diag.Add(new Diag(isError, code, text, startLine, startCharacter, endLine, endCharacter));
        }

        public static TSFileAdditionalInfo Get(IFileCache file, IDiskCache diskCache)
        {
            if (file == null) return null;
            if (file.AdditionalInfo == null)
                file.AdditionalInfo = new TSFileAdditionalInfo {Owner = file, DiskCache = diskCache};
            return (TSFileAdditionalInfo) file.AdditionalInfo;
        }

        public bool NeedsCompilation()
        {
            if (Output == null || _moduleImports == null || _localImports == null || LastCompilationCacheIds == null)
            {
                return true;
            }

            if (Enumerable.SequenceEqual(LastCompilationCacheIds, BuildLastCompilationCacheIds()))
            {
                return false;
            }

            return true;
        }

        public IEnumerable<int> BuildLastCompilationCacheIds()
        {
            yield return Owner.ChangeId;
            HashSet<TSFileAdditionalInfo> visited = null;
            if (_moduleImports != null)
                foreach (var module in _moduleImports)
                {
                    yield return module.PackageJsonChangeId;
                    var local = module.MainFileInfo;
                    if (visited == null)
                    {
                        visited = new HashSet<TSFileAdditionalInfo>();
                        visited.Add(this);
                    }

                    foreach (var id in local.BuildLastCompilationCacheIds(visited))
                    {
                        yield return id;
                    }
                }

            if (_localImports != null)
                foreach (var local in _localImports)
                {
                    if (visited == null)
                    {
                        visited = new HashSet<TSFileAdditionalInfo>();
                        visited.Add(this);
                    }

                    foreach (var id in local.BuildLastCompilationCacheIds(visited))
                    {
                        yield return id;
                    }
                }
        }

        IEnumerable<int> BuildLastCompilationCacheIds(HashSet<TSFileAdditionalInfo> visited)
        {
            if (!visited.Add(this))
                yield break;
            if (DtsLink != null)
            {
                yield return DtsLink.Owner.ChangeId;
            }
            else
            {
                yield return Owner.ChangeId;
            }

            if (_moduleImports != null)
                foreach (var module in _moduleImports)
                {
                    yield return module.PackageJsonChangeId;
                    var local = module.MainFileInfo;
                    foreach (var id in local.BuildLastCompilationCacheIds(visited))
                    {
                        yield return id;
                    }
                }

            if (_localImports != null)
                foreach (var local in _localImports)
                {
                    foreach (var id in local.BuildLastCompilationCacheIds(visited))
                    {
                        yield return id;
                    }
                }
        }

        public void RememberLastCompilationCacheIds()
        {
            LastCompilationCacheIds = BuildLastCompilationCacheIds().ToList();
        }

        public class Diag
        {
            public bool isError;
            public int code;
            public string text;
            public int startLine;
            public int startCharacter;
            public int endLine;
            public int endCharacter;

            public Diag(bool isError, int code, string text, int startLine, int startCharacter, int endLine,
                int endCharacter)
            {
                this.isError = isError;
                this.code = code;
                this.text = text;
                this.startLine = startLine;
                this.startCharacter = startCharacter;
                this.endLine = endLine;
                this.endCharacter = endCharacter;
            }
        }

        public string GetFromModule()
        {
            if (FromModule != null) return FromModule;
            if (MyProject != null)
            {
                FromModule = MyProject.Name;
                return FromModule;
            }

            var o = (IItemCache) Owner;
            while (o != null)
            {
                var n = o.Parent;
                if (n != null)
                {
                    if (n.Name == "node_modules")
                    {
                        FromModule = o.Name;
                        return FromModule;
                    }

                    o = n;
                }
                else
                    break;
            }

            return null;
        }
    }
}
