using Lib.DiskCache;
using Lib.TSCompiler;
using Xunit;

namespace Lib.Test;

public class ProjectOptionsTests
{
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
}