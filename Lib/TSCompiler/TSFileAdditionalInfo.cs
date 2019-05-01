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
        ImportedCss,
        Resource,
        JavaScriptAsset,
        Json
    }

    public class TSFileAdditionalInfo
    {
        public FileCompilationType Type;
        public IFileCache Owner { get; set; }
        public IDiskCache DiskCache { get; set; }
        public int[] LastCompilationCacheIds { get; set; }
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
        bool _updateNeedsCompilation;

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
                _updateNeedsCompilation = true;
                return true;
            }

            var result = !CompareLastCompilationCacheIds(LastCompilationCacheIds);
            if (result)
                _updateNeedsCompilation = true;
            return result;
        }

        bool CompareLastCompilationCacheIds(int[] lastCompilationCacheIds)
        {
            var idx = 0;
            if (idx >= lastCompilationCacheIds.Length || lastCompilationCacheIds[idx++] != Owner.ChangeId)
                return false;
            HashSet<object> visited = null;
            if (_moduleImports != null)
                foreach (var module in _moduleImports)
                {
                    if (visited == null)
                    {
                        visited = new HashSet<object>();
                        visited.Add(this);
                    }

                    if (!visited.Add(module))
                        continue;
                    if (idx >= lastCompilationCacheIds.Length ||
                        lastCompilationCacheIds[idx++] != module.PackageJsonChangeId)
                        return false;
                    var local = module.MainFileInfo;

                    if (!local.CompareLastCompilationCacheIds(lastCompilationCacheIds, ref idx, visited))
                        return false;
                }

            if (_localImports != null)
                foreach (var local in _localImports)
                {
                    if (visited == null)
                    {
                        visited = new HashSet<object>();
                        visited.Add(this);
                    }

                    if (!local.CompareLastCompilationCacheIds(lastCompilationCacheIds, ref idx, visited))
                        return false;
                }

            return true;
        }

        bool CompareLastCompilationCacheIds(int[] lastCompilationCacheIds, ref int idx,
            HashSet<object> visited)
        {
            if (!visited.Add(this))
                return true;
            if (DtsLink != null)
            {
                if (idx >= lastCompilationCacheIds.Length || lastCompilationCacheIds[idx++] != DtsLink.Owner.ChangeId)
                    return false;
            }
            else
            {
                if (idx >= lastCompilationCacheIds.Length || lastCompilationCacheIds[idx++] != Owner.ChangeId)
                    return false;
            }

            if (_moduleImports != null)
                foreach (var module in _moduleImports)
                {
                    if (!visited.Add(module))
                        continue;
                    if (idx >= lastCompilationCacheIds.Length ||
                        lastCompilationCacheIds[idx++] != module.PackageJsonChangeId)
                        return false;
                    var local = module.MainFileInfo;

                    if (!local.CompareLastCompilationCacheIds(lastCompilationCacheIds, ref idx, visited))
                        return false;
                }

            if (_localImports != null)
                foreach (var local in _localImports)
                {
                    if (!local.CompareLastCompilationCacheIds(lastCompilationCacheIds, ref idx, visited))
                        return false;
                }

            return true;
        }

        public int[] BuildLastCompilationCacheIds()
        {
            var result = new List<int>();
            result.Add(Owner.ChangeId);
            HashSet<object> visited = null;
            if (_moduleImports != null)
                foreach (var module in _moduleImports)
                {
                    if (visited == null)
                    {
                        visited = new HashSet<object>();
                        visited.Add(this);
                    }

                    if (!visited.Add(module))
                        continue;
                    result.Add(module.PackageJsonChangeId);
                    var local = module.MainFileInfo;

                    local.BuildLastCompilationCacheIds(result, visited);
                }

            if (_localImports != null)
                foreach (var local in _localImports)
                {
                    if (visited == null)
                    {
                        visited = new HashSet<object>();
                        visited.Add(this);
                    }

                    local.BuildLastCompilationCacheIds(result, visited);
                }

            return result.ToArray();
        }

        void BuildLastCompilationCacheIds(List<int> result, HashSet<object> visited)
        {
            if (!visited.Add(this))
                return;
            if (DtsLink != null)
            {
                result.Add(DtsLink.Owner.ChangeId);
            }
            else
            {
                result.Add(Owner.ChangeId);
            }

            if (_moduleImports != null)
                foreach (var module in _moduleImports)
                {
                    if (!visited.Add(module))
                        continue;
                    result.Add(module.PackageJsonChangeId);
                    var local = module.MainFileInfo;
                    local.BuildLastCompilationCacheIds(result, visited);
                }

            if (_localImports != null)
                foreach (var local in _localImports)
                {
                    local.BuildLastCompilationCacheIds(result, visited);
                }
        }

        public void RememberLastCompilationCacheIds()
        {
            if (LastCompilationCacheIds == null || _updateNeedsCompilation)
            {
                _updateNeedsCompilation = false;
                LastCompilationCacheIds = BuildLastCompilationCacheIds();
            }
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
