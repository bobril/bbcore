using Lib.DiskCache;
using Lib.Composition;
using Lib.TSCompiler;
using Lib.Utils;
using Lib.Utils.Logger;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using Xunit;

namespace Lib.Test;

public class ProjectOptionsTests
{
    [Fact]
    public void DefaultTSCompilerOptionsUseES2022WithoutDefineClassFields()
    {
        var options = new ProjectOptions().GetDefaultTSCompilerOptions();

        Assert.Equal(ScriptTarget.Es2022, options.target);
        Assert.False(options.useDefineForClassFields);
    }

    [Fact]
    public void Imports1()
    {
        var fs = new InMemoryFs();
        fs.WriteAllUtf8("/package.json", "{ imports: { '#components/*': './src/components/*' } }");
        var dc = new DiskCache.DiskCache(fs, () => fs);
        var project = new TSProject();
        project.DiskCache = dc;
        project.Owner = (IDirectoryCache)dc.TryGetItem("/")!;
        project.ProjectOptions = new ProjectOptions
        {
            Owner = project,
            ForbiddenDependencyUpdate = true
        };
        project.LoadProjectJson(true, null);
        Assert.Equal("/src/components/button", project.ResolveImports("#components/button"));
    }

    [Fact]
    public void ExcludeWatchersAreLoadedFromProjectRootConfig()
    {
        var fs = new InMemoryFs();
        fs.WriteAllUtf8("/package.json", "{ bobril: { excludeWatchers: [ 'bin', 'generated/file.ts' ] } }");
        var dc = new DiskCache.DiskCache(fs, () => fs);
        var project = new TSProject
        {
            DiskCache = dc,
            Owner = (IDirectoryCache)dc.TryGetItem("/")!,
            IsRootProject = true
        };
        project.ProjectOptions = new ProjectOptions
        {
            Owner = project,
            ForbiddenDependencyUpdate = true
        };

        project.LoadProjectJson(true, null);

        Assert.Equal(new[] { "/bin", "/generated/file.ts" }, dc.IgnoreWatcherChangesInPaths);
    }

    [Fact]
    public void FutureIsLoadedFromBbrc()
    {
        var fs = new InMemoryFs();
        fs.WriteAllUtf8("/package.json", "{}");
        fs.WriteAllUtf8("/.bbrc", "{ future: true, validate: true, gots: true }");
        var dc = new DiskCache.DiskCache(fs, () => fs);
        var project = new TSProject
        {
            DiskCache = dc,
            Owner = (IDirectoryCache)dc.TryGetItem("/")!,
            IsRootProject = true
        };
        project.ProjectOptions = new ProjectOptions
        {
            Owner = project,
            ForbiddenDependencyUpdate = true
        };

        project.LoadProjectJson(true, null);

        Assert.True(project.ProjectOptions.Future);
        Assert.True(project.ProjectOptions.Validate);
        Assert.True(project.ProjectOptions.GoTs);
        Assert.False(NjsastTsValidator.IsBuildinEnabled(project.ProjectOptions.Future, project.ProjectOptions.Validate));
        Assert.True(NjsastTsValidator.IsEnabled(project.ProjectOptions.Validate));
    }

    [Fact]
    public void NativeTypeScriptDirectoryCanBeFoundInParentNodeModules()
    {
        var fs = new InMemoryFs();
        fs.WriteAllUtf8("/workspace/node_modules/@typescript/native-preview/bin/tsgo.js", "");

        Assert.True(BuildCtx.TryFindNativeTypeScriptDirectory("/workspace/apps/WebApp", fs, out var directory));
        Assert.Equal("/workspace/node_modules/@typescript/native-preview", directory);
    }

    [Fact]
    public void GeneratedTsConfigIncludesConfiguredTestSources()
    {
        var fs = new InMemoryFs();
        fs.WriteAllUtf8("/package.json", "{ main: 'src/index.ts', bobril: { testDirectories: [ 'spec' ] } }");
        fs.WriteAllUtf8("/src/index.ts", "export const value = 1;");
        fs.WriteAllUtf8("/spec/test.spec.ts", "describe('test', () => {});");
        var dc = new DiskCache.DiskCache(fs, () => fs);
        var project = TSProject.Create((IDirectoryCache)dc.TryGetItem("/")!, dc, new DummyLogger(), null)!;
        project.IsRootProject = true;
        project.ProjectOptions!.Tools = new ToolsDir.ToolsDir(
            PathUtils.Join(PathUtils.Normalize(Environment.CurrentDirectory), ".bbcore/tools"), new DummyLogger(),
            new NativeFsAbstraction());
        project.ProjectOptions.ForbiddenDependencyUpdate = true;

        project.LoadProjectJson(true, null);
        project.ProjectOptions.RefreshCompilerOptions();
        project.ProjectOptions.RefreshMainFile();
        project.ProjectOptions.RefreshTestSources();
        project.ProjectOptions.UpdateTSConfigJson();

        var tsconfig = JObject.Parse(fs.ReadAllUtf8("/tsconfig.json"));
        var files = tsconfig["files"]!.Values<string>().ToArray();
        Assert.Contains("src/index.ts", files);
        Assert.Contains("spec/test.spec.ts", files);
    }
}
