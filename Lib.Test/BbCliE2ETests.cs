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
    [Theory]
    [InlineData(false, "no")]
    [InlineData(true, "yes")]
    public void TestFilePathBuildsOnlySelectedTestAndItsDependencies(bool absolutePath, string typeCheck)
    {
        var bbDll = Path.Combine(FindRepoRoot(), "bb", "bin", "Debug", "net10.0", "bb.dll");
        var projectDir = Path.Combine(Path.GetTempPath(), "bbcore-test-file-e2e-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(projectDir, "spec"));
        try
        {
            File.WriteAllText(Path.Combine(projectDir, "package.json"),
                """{"name":"test-file-e2e","main":"index.ts","bobril":{"dependencies":"disabled"}}""");
            File.WriteAllText(Path.Combine(projectDir, "index.ts"), "export const value = 1;");
            File.WriteAllText(Path.Combine(projectDir, "spec", "helper.ts"),
                "export const value = 'selected-dependency-marker';");
            File.WriteAllText(Path.Combine(projectDir, "spec", "selected.spec.ts"),
                """
                import { value } from './helper';
                describe('selected-suite-marker', () => {
                    it('works', () => expect(value).toBe('selected-dependency-marker'));
                });
                """);
            // This import would fail the build if the unrelated test were compiled.
            File.WriteAllText(Path.Combine(projectDir, "spec", "other.spec.ts"),
                "import './missing-dependency'; describe('unrelated-suite-marker', () => {});");
            var testPath = absolutePath ? Path.Combine(projectDir, "spec", "selected.spec.ts") : "spec/selected.spec.ts";

            RunBb(bbDll, projectDir, "test", "--testFilePath", testPath,
                "--filter", "^selected-suite-marker works$", "--dir", "dist", "-t", typeCheck);

            var distDir = Path.Combine(projectDir, "dist");
            Assert.True(File.Exists(Path.Combine(distDir, "test.html")));
            var bundle = string.Join("\n", Directory.EnumerateFiles(distDir, "*.js", SearchOption.AllDirectories)
                .Select(File.ReadAllText));
            Assert.Contains("selected-suite-marker", bundle);
            Assert.Contains("selected-dependency-marker", bundle);
            Assert.DoesNotContain("unrelated-suite-marker", bundle);
        }
        finally
        {
            DeleteFixtureProject(projectDir);
        }
    }

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
