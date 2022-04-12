using Lib.DiskCache;
using Lib.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Reflection;
using Lib.Registry;
using Lib.Utils.Logger;

namespace Lib.TSCompiler;

public class TSProject
{
    public ILogger Logger { get; set; }
    public IDiskCache DiskCache { get; set; }
    public IDirectoryCache Owner { get; set; }
    public string MainFile { get; set; }

    public string TypesMainFile { get; set; }
    public ProjectOptions? ProjectOptions { get; set; }
    public int PackageJsonChangeId { get; set; }
    public bool IsRootProject { get; set; }

    public HashSet<string>? Dependencies;
    public HashSet<string>? DevDependencies;
    public HashSet<string>? UsedDependencies;
    public Dictionary<string, string>? Assets;
    public string? Name;
    internal int IterationId;
    internal BTDB.Collections.StructList<string> NegativeChecks;
    internal bool Valid;
    internal bool Virtual;

    public void LoadProjectJson(bool forbiddenDependencyUpdate, ProjectOptions? parentProjectOptions)
    {
        if (Virtual)
        {
            PackageJsonChangeId = -1;
            Dependencies = new();
            DevDependencies = new();
            Assets = null;
            return;
        }
        DiskCache.UpdateIfNeeded(Owner);
        var packageJsonFile = Owner.TryGetChild("package.json");
        if (packageJsonFile is IFileCache cache)
        {
            var newChangeId = cache.ChangeId;
            if (newChangeId == PackageJsonChangeId) return;

            var rp = PathUtils.RealPath(packageJsonFile.FullPath);
            if (rp != packageJsonFile.FullPath)
            {
                if (DiskCache.TryGetItem(rp) is IFileCache realPackageJsonFile)
                {
                    Owner = realPackageJsonFile.Parent!;
                    Owner.Project = this;
                    cache = realPackageJsonFile;
                    newChangeId = cache.ChangeId;
                }
            }

            ProjectOptions.FinalCompilerOptions = null;
            JObject parsed;
            try
            {
                parsed = JObject.Parse(cache.Utf8Content);
            }
            catch (Exception)
            {
                parsed = new();
            }

            var deps = new HashSet<string>();
            var devdeps = new HashSet<string>();
            var hasMain = false;
            if (parsed.GetValue("typescript") is JObject parsedT)
            {
                if (parsedT.GetValue("main") is JValue mainV)
                {
                    MainFile = PathUtils.Normalize(mainV.ToString());
                    if (DiskCache.TryGetItem(PathUtils.Join(Owner.FullPath, MainFile)) is IFileCache)
                    {
                        TypesMainFile = null;
                        hasMain = true;
                    }
                }
            }

            if (!hasMain && parsed.GetValue("browser") is JValue browserMain)
            {
                MainFile = PathUtils.Normalize(browserMain.ToString());
                if (DiskCache.TryGetItem(PathUtils.Join(Owner.FullPath, MainFile)) is IFileCache)
                {
                    hasMain = true;
                }
            }

            var name = parsed.GetValue("name") is JValue vname ? vname.ToString() : "";

            if (!hasMain && parsed.GetValue("module") is JValue moduleV)
            {
                if (name != "moment")
                {
                    MainFile = PathUtils.Normalize(moduleV.ToString());
                    if (DiskCache.TryGetItem(PathUtils.Join(Owner.FullPath, MainFile)) is IFileCache)
                    {
                        hasMain = true;
                    }
                }
            }

            if (parsed.GetValue("typings") is JValue typingsV)
            {
                TypesMainFile = PathUtils.Normalize(typingsV.ToString());
            }

            if (!hasMain)
            {
                if (parsed.GetValue("main") is JValue mainV2)
                {
                    MainFile = PathUtils.Normalize(mainV2.ToString());
                    if (PathUtils.GetExtension(MainFile).IsEmpty)
                    {
                        MainFile += ".js";
                    }
                }
                else
                {
                    MainFile = "index.js";
                }

                if (name == "@stomp/stompjs")
                {
                    MainFile = "esm6/index.js";
                    hasMain = true;
                }

                if (DiskCache.TryGetItem(PathUtils.Join(Owner.FullPath, PathUtils.ChangeExtension(MainFile, "ts")))
                    is IFileCache fileAsTs)
                {
                    MainFile = PathUtils.ChangeExtension(MainFile, "ts");
                    TypesMainFile = null;
                    hasMain = true;
                }
                else
                {
                    fileAsTs = DiskCache.TryGetItem(PathUtils.Join(Owner.FullPath,
                        PathUtils.ChangeExtension(MainFile, "tsx"))) as IFileCache;
                    if (fileAsTs != null)
                    {
                        MainFile = PathUtils.ChangeExtension(MainFile, "tsx");
                        TypesMainFile = null;
                        hasMain = true;
                    }
                }

                if (!hasMain)
                {
                    if (parsed.GetValue("types") is JValue mainV)
                    {
                        TypesMainFile = PathUtils.Normalize(mainV.ToString());
                        hasMain = true;
                    }
                }
            }

            if (TypesMainFile == null)
            {
                TypesMainFile = PathUtils.ChangeExtension(MainFile, "d.ts");
                if (!IsRootProject &&
                    !(DiskCache.TryGetItem(PathUtils.Join(Owner.FullPath, TypesMainFile)) is IFileCache))
                {
                    var typesDts = PathUtils.Join(Owner.FullPath, $"../@types/{Owner.Name}/index.d.ts");
                    if (DiskCache.TryGetItem(typesDts) is IFileCache)
                    {
                        TypesMainFile = typesDts;
                    }
                }
            }

            if (parsed.GetValue("dependencies") is JObject parsedV)
            {
                foreach (var i in parsedV.Properties())
                {
                    deps.Add(i.Name);
                }
            }

            if (parsed.GetValue("devDependencies") is JObject parsedV2)
            {
                foreach (var i in parsedV2.Properties())
                {
                    devdeps.Add(i.Name);
                }
            }

            PackageJsonChangeId = newChangeId;
            Dependencies = deps;
            DevDependencies = devdeps;

            if (ProjectOptions == null)
            {
                Assets = ParseBobrilAssets(parsed, Owner);
                return;
            }
            ProjectOptions.FillProjectOptionsFromPackageJson(parsed, Owner);
            Assets = ProjectOptions.Assets;
            if (forbiddenDependencyUpdate || ProjectOptions.DependencyUpdate == DepedencyUpdate.Disabled) return;
            var packageManager = new CurrentNodePackageManager(DiskCache, Logger);
            if (ProjectOptions.DependencyUpdate == DepedencyUpdate.Upgrade)
            {
                packageManager.UpgradeAll(Owner);
            }
            else
            {
                packageManager.Install(Owner);
            }

            DiskCache.CheckForTrueChange();
            DiskCache.ResetChange();
        }
        else
        {
            PackageJsonChangeId = -1;
            MainFile = "index.js";
            Dependencies = new();
            DevDependencies = new();
            Assets = null;
            ProjectOptions?.FillProjectOptionsFromPackageJson(null, Owner);
        }
    }

    Dictionary<string, string>? ParseBobrilAssets(JObject parsed, IDirectoryCache? dir)
    {
        if (parsed?.GetValue("bobril") is not JObject bobrilSection)
            return null;
        var bbOptions = new BobrilBuildOptions(bobrilSection);
        bbOptions = ProjectOptions.LoadBbrc(dir, bbOptions);
        return bbOptions.assets;
    }

    public static TSProject? Create(IDirectoryCache? dir, IDiskCache diskCache, ILogger logger, string? diskName, bool virtualProject = false)
    {
        if (dir == null)
            return null;
        if (dir.Project != null)
            return (TSProject)dir.Project;
        if (diskName == null)
        {
            if (dir.Parent?.Name.StartsWith("@") ?? false)
            {
                diskName = dir.Parent.Name + "/" + dir.Name;
            }
            else
            {
                diskName = dir.Name;
            }
        }

        var proj = new TSProject
        {
            Owner = dir,
            DiskCache = diskCache,
            Logger = logger,
            Name = diskName,
            Valid = true,
            ProjectOptions = new(),
            Virtual = virtualProject
        };
        proj.ProjectOptions.Owner = proj;
        if (virtualProject)
            proj.ProjectOptions.FillProjectOptionsFromPackageJson(null, dir);
        else
            dir.Project = proj;

        return proj;
    }

    internal static TSProject CreateInvalid(string name)
    {
        var proj = new TSProject
        {
            Name = name,
            Valid = false
        };
        return proj;
    }

    public void FillOutputByAssets(MainBuildResult buildResult,
        string nodeModulesDir, ProjectOptions projectOptions)
    {
        if (Assets == null) return;
        foreach (var asset in Assets)
        {
            var fromModules = asset.Key.StartsWith("node_modules/");
            var fullPath = fromModules ? nodeModulesDir : Owner.FullPath;
            if (fromModules)
            {
                projectOptions.Owner.UsedDependencies ??= new();
                var pos = 0;
                PathUtils.EnumParts(asset.Key, ref pos, out var name, out _);
                PathUtils.EnumParts(asset.Key, ref pos, out name, out _);
                projectOptions.Owner.UsedDependencies.Add(name.ToString());
            }

            var item = DiskCache.TryGetItem(PathUtils.Join(fullPath, asset.Key));
            if (item == null || item.IsInvalid)
                continue;
            if (item is IFileCache)
            {
                buildResult.TakenNames.Add(asset.Value);
                buildResult.FilesContent.GetOrAddValueRef(asset.Value) = new Lazy<object>(() =>
                {
                    var res = ((IFileCache) item).ByteContent;
                    ((IFileCache) item).FreeCache();
                    return res;
                });
            }
            else
            {
                RecursiveAddFilesContent((IDirectoryCache) item, buildResult, asset.Value);
            }
        }
    }

    void RecursiveAddFilesContent(IDirectoryCache directory, MainBuildResult buildResult, string destDir)
    {
        DiskCache.UpdateIfNeeded(directory);
        foreach (var child in directory)
        {
            if (child.IsInvalid)
                continue;
            var outPathFileName = (destDir != "" ? destDir + "/" : "") + child.Name;
            buildResult.TakenNames.Add(outPathFileName);
            switch (child)
            {
                case IDirectoryCache directoryCache:
                    RecursiveAddFilesContent(directoryCache, buildResult, outPathFileName);
                    continue;
                case IFileCache fileCache:
                    buildResult.FilesContent.GetOrAddValueRef(outPathFileName) =
                        new Lazy<object>(() =>
                        {
                            var res = fileCache.ByteContent;
                            fileCache.FreeCache();
                            return res;
                        });
                    break;
            }
        }
    }
}
