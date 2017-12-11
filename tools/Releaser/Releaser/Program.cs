using Octokit;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Releaser
{
    class Program
    {
        static void Main(string[] args)
        {
            MainAsync(args).Wait();
        }

        static async Task<int> MainAsync(string[] args)
        {
            var projDir = Environment.CurrentDirectory;
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
            var logLines = File.ReadAllLines(projDir + "/CHANGELOG.md");
            var topVersion = logLines.Where(s => s.StartsWith("## ")).FirstOrDefault();
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
            var topVersionLine = Array.IndexOf(logLines, topVersion);
            var lastVersionLine = Array.IndexOf(logLines, lastVersion);
            var lastVersionNumber = new Version(lastVersion.Substring(3));
            var patchVersionNumber = new Version(lastVersionNumber.Major, lastVersionNumber.Minor, lastVersionNumber.Build + 1);
            var minorVersionNumber = new Version(lastVersionNumber.Major, lastVersionNumber.Minor + 1, 0);
            var majorVersionNumber = new Version(lastVersionNumber.Major + 1, 0, 0);
            Console.WriteLine("Press 1 for Major " + majorVersionNumber.ToString(3));
            Console.WriteLine("Press 2 for Minor " + minorVersionNumber.ToString(3));
            Console.WriteLine("Press 3 for Patch " + patchVersionNumber.ToString(3));
            var choice = Console.ReadKey().KeyChar;
            Console.WriteLine();
            if (choice < '1' || choice > '3')
            {
                Console.WriteLine("Not pressed 1, 2 or 3. Exiting.");
                return 1;
            }
            if (choice == '1') lastVersionNumber = majorVersionNumber;
            if (choice == '2') lastVersionNumber = minorVersionNumber;
            if (choice == '3') lastVersionNumber = patchVersionNumber;
            var newVersion = lastVersionNumber.ToString(3);
            Console.WriteLine("Building version " + newVersion);
            var outputLogLines = logLines.ToList();
            var releaseLogLines = logLines.Skip(topVersionLine + 1).SkipWhile(s => string.IsNullOrWhiteSpace(s)).TakeWhile(s => !s.StartsWith("## ")).ToList();
            while (releaseLogLines.Count > 0 && string.IsNullOrWhiteSpace(releaseLogLines[releaseLogLines.Count - 1])) releaseLogLines.RemoveAt(releaseLogLines.Count - 1);
            outputLogLines.Insert(topVersionLine + 1, "## " + newVersion);
            outputLogLines.Insert(topVersionLine + 1, "");
            //File.WriteAllText(projDir + "/CHANGELOG.md", string.Join("", outputLogLines.Select(s => s + '\n')));
            if (Directory.Exists(projDir + "/bb/bin/Release/netcoreapp2.0"))
                Directory.Delete(projDir + "/bb/bin/Release/netcoreapp2.0", true);
            var start = new ProcessStartInfo("dotnet", "publish -c Release -r win10-x64 /p:ShowLinkerSizeComparison=true /p:Version=" + newVersion + ".0")
            {
                UseShellExecute = true,
                WorkingDirectory = projDir+"/bb"
            };
            var process = Process.Start(start);
            process.WaitForExit();
            if (Directory.Exists(projDir+ "/bb/bin/Release/netcoreapp2.0/win10-x64/publish/ru-ru"))
                Directory.Delete(projDir + "/bb/bin/Release/netcoreapp2.0/win10-x64/publish/ru-ru", true);
            System.IO.Compression.ZipFile.CreateFromDirectory(projDir + "/bb/bin/Release/netcoreapp2.0/win10-x64/publish", projDir + "/bb/bin/Release/netcoreapp2.0/win10-x64.zip", System.IO.Compression.CompressionLevel.Optimal, false);
            var client = new GitHubClient(new ProductHeaderValue("bobril-bbcore-releaser"));
            var fileNameOfToken = Environment.GetEnvironmentVariable("USERPROFILE") + "/.github/token.txt";
            string token;
            try
            {
                token = File.ReadAllLines(fileNameOfToken).First();
            }
            catch
            {
                Console.WriteLine("Cannot read github token from " + fileNameOfToken);
                return 1;
            }
            client.Credentials = new Credentials(token);
            var bbcoreRepo = (await client.Repository.GetAllForOrg("bobril")).First(r => r.Name == "bbcore");
            Console.WriteLine("bbcore repo id: " + bbcoreRepo.Id);
            var release = new NewRelease(newVersion);
            //client.Repository.Release.Create(bbcoreRepo.Id, release);
            Console.ReadLine();
            return 0;
        }
    }
}
