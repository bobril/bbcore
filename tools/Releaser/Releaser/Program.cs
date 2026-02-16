using LibGit2Sharp;
using Octokit;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Releaser;

// colima start --vm-type=vz --vz-rosetta --cpu 4 --memory 16 --disk 100

static class Program
{
    static void Main()
    {
        MainAsync().Wait();
    }

    static readonly string[] Rids = ["win-x64", "win-arm64", "linux-x64", "osx-x64", "osx-arm64"];

    static async Task<int> MainAsync()
    {
        var projDir = Environment.CurrentDirectory;
        var colimaStartedByReleaser = false;
        try
        {
            while (!File.Exists(projDir + "/CHANGELOG.md"))
            {
                projDir = Path.GetDirectoryName(projDir);
                if (string.IsNullOrWhiteSpace(projDir))
                {
                    Console.WriteLine("Cannot find CHANGELOG.md in some parent directory");
                    return 1;
                }
            }

            Console.WriteLine("Project root directory: " + projDir);
            var logLines = await File.ReadAllLinesAsync(projDir + "/CHANGELOG.md");
            var topVersion = logLines.FirstOrDefault(s => s.StartsWith("## "));
            var lastVersion = logLines.Where(s => s.StartsWith("## ")).Skip(1).FirstOrDefault();
            if (logLines.Length < 5)
            {
                Console.WriteLine("CHANGELOG.md has less than 5 lines");
                return 1;
            }

            if (topVersion != "## [unreleased]")
            {
                Console.WriteLine("Top version should be ## [unreleased]");
                return 1;
            }

            if (lastVersion == null)
            {
                Console.WriteLine("Cannot find previous version");
                return 1;
            }

            using var gitrepo = new LibGit2Sharp.Repository(projDir);
            int workDirChangesCount;
            using (var workDirChanges = gitrepo.Diff.Compare<TreeChanges>())
                workDirChangesCount = workDirChanges.Count;
            if (workDirChangesCount > 0)
            {
                Console.WriteLine("DANGER! THERE ARE " + workDirChangesCount + " CHANGES IN WORK DIR!");
            }

            var topVersionLine = Array.IndexOf(logLines, topVersion);
            var lastVersionNumber = new System.Version(lastVersion[3..]);
            var patchVersionNumber = new System.Version(lastVersionNumber.Major, lastVersionNumber.Minor,
                lastVersionNumber.Build + 1);
            var minorVersionNumber = new System.Version(lastVersionNumber.Major, lastVersionNumber.Minor + 1, 0);
            var majorVersionNumber = new System.Version(lastVersionNumber.Major + 1, 0, 0);
            Console.WriteLine("Press 1 for Major " + majorVersionNumber.ToString(3));
            Console.WriteLine("Press 2 for Minor " + minorVersionNumber.ToString(3));
            Console.WriteLine("Press 3 for Patch " + patchVersionNumber.ToString(3));
            Console.WriteLine("Press 4 for Just Docker Build");
            var choice = Console.ReadKey().KeyChar;
            Console.WriteLine();
            if (choice is < '1' or > '4')
            {
                Console.WriteLine("Not pressed 1, 2, 3 or 4. Exiting.");
                return 1;
            }

            if (choice == '1')
                lastVersionNumber = majorVersionNumber;
            if (choice == '2')
                lastVersionNumber = minorVersionNumber;
            if (choice == '3')
                lastVersionNumber = patchVersionNumber;
            var newVersion = lastVersionNumber.ToString(3);
            if (choice == '4')
            {
                Console.WriteLine("Building just docker version " + newVersion);
            }
            else
            {
                var fileNameOfNugetToken =
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/.nuget/token.txt";
                string nugetToken;
                try
                {
                    nugetToken = File.ReadAllLines(fileNameOfNugetToken).First();
                }
                catch
                {
                    Console.WriteLine("Cannot read nuget token from " + fileNameOfNugetToken);
                    return 1;
                }

                Console.WriteLine("Building version " + newVersion);
                var outputLogLines = logLines.ToList();
                var releaseLogLines = logLines.Skip(topVersionLine + 1).SkipWhile(string.IsNullOrWhiteSpace)
                    .TakeWhile(s => !s.StartsWith("## ")).ToList();
                while (releaseLogLines.Count > 0 && string.IsNullOrWhiteSpace(releaseLogLines[^1]))
                    releaseLogLines.RemoveAt(releaseLogLines.Count - 1);
                outputLogLines.Insert(topVersionLine + 1, "## " + newVersion);
                outputLogLines.Insert(topVersionLine + 1, "");
                if (Directory.Exists(projDir + "/bb/bin/Release/net10.0"))
                    Directory.Delete(projDir + "/bb/bin/Release/net10.0", true);
                await UpdateCsProj(projDir, newVersion);
                BuildNuget(projDir, newVersion, nugetToken);
                foreach (var rid in Rids)
                {
                    Build(projDir, newVersion, rid);
                }

                var client = new GitHubClient(new ProductHeaderValue("bobril-bbcore-releaser"));
                client.SetRequestTimeout(TimeSpan.FromMinutes(15));
                var fileNameOfToken =
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/.github/token.txt";
                string token;
                try
                {
                    token = (await File.ReadAllLinesAsync(fileNameOfToken)).First();
                }
                catch
                {
                    Console.WriteLine("Cannot read github token from " + fileNameOfToken);
                    return 1;
                }

                client.Credentials = new(token);
                var bbcoreRepo = (await client.Repository.GetAllForOrg("bobril")).First(r => r.Name == "bbcore");
                Console.WriteLine("bbcore repo id: " + bbcoreRepo.Id);
                await File.WriteAllTextAsync(projDir + "/CHANGELOG.md",
                    string.Join("", outputLogLines.Select(s => s + '\n')));
                Commands.Stage(gitrepo, "CHANGELOG.md");
                Commands.Stage(gitrepo, "Bbcore.Lib/Bbcore.Lib.csproj");
                var author = new LibGit2Sharp.Signature("Releaser", "releaser@bobril.com", DateTime.Now);
                gitrepo.Commit("Released " + newVersion, author, author);
                gitrepo.ApplyTag(newVersion);
                var options = new PushOptions
                {
                    CredentialsProvider = (_, _, _) =>
                        new UsernamePasswordCredentials
                        {
                            Username = token,
                            Password = ""
                        }
                };
                gitrepo.Network.Push(gitrepo.Head, options);
                var release = new NewRelease(newVersion)
                {
                    Name = newVersion,
                    Body = string.Join("", releaseLogLines.Select(s => s + '\n')),
                    Prerelease = true
                };
                var release2 = await client.Repository.Release.Create(bbcoreRepo.Id, release);
                Console.WriteLine("release url:");
                Console.WriteLine(release2.HtmlUrl);
                foreach (var rid in Rids)
                {
                    var uploadAsset = await UploadWithRetry(projDir, client, release2, ToZipName(rid) + ".zip");
                    Console.WriteLine(ToZipName(rid) + " url:");
                    Console.WriteLine(uploadAsset.BrowserDownloadUrl);
                }
            }

            colimaStartedByReleaser = EnsureDockerRunning(projDir);
            DockerBuild(projDir, newVersion);
            Console.WriteLine("Press Enter for finish");
            Console.ReadLine();
            return 0;
        }
        finally
        {
            if (colimaStartedByReleaser)
            {
                StopColima(projDir);
            }
        }
    }

    static async Task UpdateCsProj(string projDir, string newVersion)
    {
        var fn = projDir + "/Bbcore.Lib/Bbcore.Lib.csproj";
        var content = await File.ReadAllTextAsync(fn);
        content = new Regex("<Version>.+</Version>").Replace(content, "<Version>" + newVersion + "</Version>");
        await File.WriteAllTextAsync(fn, content, new UTF8Encoding(false));
    }

    static void BuildNuget(string projDir, string newVersion, string nugetToken)
    {
        var start = new ProcessStartInfo("dotnet", "pack -c Release")
        {
            UseShellExecute = true,
            WorkingDirectory = projDir + "/Bbcore.Lib"
        };
        var process = Process.Start(start);
        process!.WaitForExit();
        start = new("dotnet", "nuget push Bbcore.Lib." + newVersion + ".nupkg -s https://nuget.org -k " + nugetToken)
        {
            UseShellExecute = true,
            WorkingDirectory = projDir + "/Bbcore.Lib/bin/Release"
        };
        process = Process.Start(start);
        process!.WaitForExit();
    }

    static void DockerBuild(string projDir, string version)
    {
        try
        {
            RunDocker(projDir, $"buildx build --platform linux/amd64 -t bobril/build --build-arg VERSION={version} .");
            RunDocker(projDir, $"tag bobril/build bobril/build:{version}");
            RunDocker(projDir, $"push bobril/build:{version}");
            RunDocker(projDir, $"push bobril/build:latest");
        }
        catch (Exception)
        {
            Console.WriteLine("Docker build failed");
        }
    }

    static bool EnsureDockerRunning(string projDir)
    {
        if (IsDockerRunning(projDir))
        {
            return false;
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            throw new("Docker is not running and automatic Colima start is supported only on macOS.");
        }

        Console.WriteLine("Docker is not running. Starting Colima.");
        RunCommand(projDir, "colima", "start --vm-type=vz --vz-rosetta --cpu 4 --memory 16 --disk 100");
        if (!IsDockerRunning(projDir))
        {
            throw new("Docker is still not running after starting Colima.");
        }

        Console.WriteLine("Docker is running after Colima start.");
        return true;
    }

    static bool IsDockerRunning(string projDir)
    {
        try
        {
            var start = new ProcessStartInfo("docker", "info -f {{.ServerVersion}}")
            {
                UseShellExecute = true,
                WorkingDirectory = projDir
            };
            var process = Process.Start(start);
            process!.WaitForExit();
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Docker check failed: " + ex.Message);
            return false;
        }
    }

    static void StopColima(string projDir)
    {
        try
        {
            Console.WriteLine("Stopping Colima started by releaser.");
            RunCommand(projDir, "colima", "stop");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Failed to stop Colima: " + ex.Message);
        }
    }

    static void RunCommand(string projDir, string fileName, string arguments)
    {
        var start = new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = true,
            WorkingDirectory = projDir
        };
        Console.WriteLine($"Starting {fileName} {start.Arguments}");
        var process = Process.Start(start);
        process!.WaitForExit();
        if (process.ExitCode > 0)
        {
            Console.WriteLine($"Exit code:{process.ExitCode}");
            throw new($"{fileName} failed");
        }
    }

    static void RunDocker(string projDir, string command)
    {
        RunCommand(projDir, "docker", command);
    }

    static async Task<ReleaseAsset> UploadWithRetry(string projDir, GitHubClient client, Release release2,
        string fileName)
    {
        for (var i = 0; i < 5; i++)
        {
            try
            {
                return await client.Repository.Release.UploadAsset(release2,
                    new(fileName, "application/zip",
                        File.OpenRead(projDir + "/bb/bin/Release/net10.0/" + fileName), TimeSpan.FromMinutes(14)));
            }
            catch (Exception)
            {
                Console.WriteLine("Upload Asset " + fileName + " failed " + i);
            }
        }

        throw new OperationCanceledException("Upload Asset " + fileName + " failed");
    }

    static void Build(string projDir, string newVersion, string rid)
    {
        var start = new ProcessStartInfo("dotnet",
            $"publish -c Release -r {rid} --self-contained true -p:DebugType=None -p:DebugSymbols=false -p:Version=" +
            newVersion + ".0")
        {
            UseShellExecute = true,
            WorkingDirectory = projDir + "/bb"
        };
        var process = Process.Start(start);
        process!.WaitForExit();
        if (Directory.Exists(projDir + $"/bb/bin/Release/net10.0/{rid}/publish/ru-ru"))
        {
            Directory.Delete(projDir + $"/bb/bin/Release/net10.0/{rid}/publish/ru-ru", true);
        }

        if (!rid.StartsWith("win"))
        {
            if (Directory.Exists(projDir + $"/bb/bin/Release/net10.0/{rid}/publish/Resources"))
            {
                Directory.Delete(projDir + $"/bb/bin/Release/net10.0/{rid}/publish/Resources", true);
            }
        }

        if (rid == "osx-arm64")
        {
            File.Copy(projDir + "/tools/Releaser/Releaser/OsxArm64/bb",
                projDir + $"/bb/bin/Release/net10.0/{rid}/publish/bb", true);
            Console.WriteLine("Overwritten Osx Arm64 bb to be signed");
        }

        System.IO.Compression.ZipFile.CreateFromDirectory(projDir + $"/bb/bin/Release/net10.0/{rid}/publish",
            projDir + $"/bb/bin/Release/net10.0/{ToZipName(rid)}.zip", System.IO.Compression.CompressionLevel.Optimal,
            false);
    }

    static string ToZipName(string rid)
    {
        return rid;
    }
}
