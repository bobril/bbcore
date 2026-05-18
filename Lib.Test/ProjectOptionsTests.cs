using Lib.DiskCache;
using Lib.TSCompiler;
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
        fs.WriteAllUtf8("/.bbrc", "{ future: true }");
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
    }
}
