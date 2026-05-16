using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace Test;

static class TestEnvironment
{
    [ModuleInitializer]
    public static void Initialize()
    {
        Directory.SetCurrentDirectory(ProjectRoot);
    }

    static string ProjectRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Njsast.Test.csproj"))
                    && Directory.Exists(Path.Combine(directory.FullName, "Input")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Could not find Njsast.Test project root containing Input fixtures");
        }
    }
}
