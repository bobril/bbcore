using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;

namespace UpdateWebUI;

class Program
{
    static int Main(string[] args)
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
        Build(projDir + "/tools/web", projDir + "/Lib/ToolsDir/web.zip");
        Build(projDir + "/tools/webt", projDir + "/Lib/ToolsDir/webt.zip");
        Build(projDir + "/tools/CoverageDetailsVisualizer", projDir + "/Lib/ToolsDir/CoverageDetailsVisualizer.zip");
        return 0;
    }

    static void Build(string projDir, string targetZip)
    {
        var start = new ProcessStartInfo("bb", "b")
        {
            UseShellExecute = true,
            WorkingDirectory = projDir
        };
        Console.WriteLine($"Starting bobril-build of {projDir}");
        var process = Process.Start(start);
        process.WaitForExit();
        if (process.ExitCode > 0)
        {
            Console.WriteLine($"Exit code:{process.ExitCode}");
            return;
        }

        File.Delete(targetZip);
        ZipFile.CreateFromDirectory(projDir + "/dist", targetZip, CompressionLevel.Optimal, false);
    }
}