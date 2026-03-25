using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace Lib.Test;

[Collection("Serial")]
public class BbCliE2ETests
{
    [Fact]
    public void BuildCommandsWorkForProjectUsingBobrilG11n()
    {
        var repoRoot = FindRepoRoot();
        var bbDll = Path.Combine(repoRoot, "bb", "bin", "Debug", "net10.0", "bb.dll");
        Assert.True(File.Exists(bbDll), $"bb executable not found at {bbDll}");

        var projectDir = PrepareFixtureProject(repoRoot);
        try
        {
            RunBb(bbDll, projectDir, "build", "-f", "1");
            AssertBuildOutput(projectDir);

            var distDir = Path.Combine(projectDir, "dist");
            if (Directory.Exists(distDir))
                Directory.Delete(distDir, true);

            RunBb(bbDll, projectDir, "build");
            AssertBuildOutput(projectDir);
        }
        finally
        {
            DeleteFixtureProject(projectDir);
        }
    }

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "bb", "bb.csproj")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    static string PrepareFixtureProject(string repoRoot)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bbcore-g11n-e2e-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        CopyDirectory(Path.Combine(repoRoot, "TestProjects", "G11nBuildE2E"), tempDir);
        var sourceNodeModulesDir = Path.Combine(repoRoot, "TestProjects", "BbApp", "node_modules");
        var tempNodeModulesDir = Path.Combine(tempDir, "node_modules");
        Directory.CreateDirectory(tempNodeModulesDir);
        Directory.CreateSymbolicLink(
            Path.Combine(tempNodeModulesDir, "bobril"),
            Path.Combine(sourceNodeModulesDir, "bobril"));
        Directory.CreateSymbolicLink(
            Path.Combine(tempNodeModulesDir, "moment"),
            Path.Combine(sourceNodeModulesDir, ".pnpm", "moment@2.30.1", "node_modules", "moment"));
        CopyDirectory(
            Path.Combine(sourceNodeModulesDir, "bobril-g11n"),
            Path.Combine(tempNodeModulesDir, "bobril-g11n"));
        var formatterPath = Path.Combine(tempNodeModulesDir, "bobril-g11n", "src", "msgFormatter.ts");
        var formatterContent = File.ReadAllText(formatterPath);
        formatterContent = formatterContent.Replace(
            "import * as moment from \"moment\";",
            "import moment from \"moment\";",
            StringComparison.Ordinal);
        File.WriteAllText(formatterPath, formatterContent);

        return tempDir;
    }

    static void AssertBuildOutput(string projectDir)
    {
        var distDir = Path.Combine(projectDir, "dist");
        Assert.True(Directory.Exists(distDir), "dist directory was not created.");
        Assert.True(Directory.EnumerateFiles(distDir, "*.js", SearchOption.TopDirectoryOnly).Any(),
            "No JavaScript bundle was produced.");
        Assert.True(File.Exists(Path.Combine(distDir, "index.html")), "index.html was not produced.");
    }

    static void DeleteFixtureProject(string projectDir)
    {
        if (Directory.Exists(projectDir))
            Directory.Delete(projectDir, true);
    }

    static void RunBb(string bbDll, string workingDirectory, params string[] arguments)
    {
        var fullArguments = new string[arguments.Length + 1];
        fullArguments[0] = bbDll;
        Array.Copy(arguments, 0, fullArguments, 1, arguments.Length);
        RunProcess("dotnet", workingDirectory, fullArguments);
    }

    static void RunProcess(string fileName, string workingDirectory, params string[] arguments)
    {
        var output = new StringBuilder();
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };
        foreach (var argument in arguments)
            process.StartInfo.ArgumentList.Add(argument);

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) output.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) output.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        Assert.True(process.ExitCode == 0,
            $"Command failed: {fileName} {string.Join(" ", arguments)}{Environment.NewLine}output:{Environment.NewLine}{output}");
    }

    static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);
        foreach (var directory in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(directory.Replace(sourceDir, destinationDir, StringComparison.Ordinal));
        }

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, file.Replace(sourceDir, destinationDir, StringComparison.Ordinal), true);
        }
    }
}
