using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Lib.DiskCache;
using Lib.Utils.Logger;

namespace bb;

class Program
{
    static async Task Main(string[] args)
    {
        var composition = new Lib.Composition.Composition(
            inDocker: Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") != null,
            new ConsoleLogger(),
            new NativeFsAbstraction());

        composition.ParseCommandLine(args);
        await composition.RunCommand();
        SkipNativeDestructorsOnExit();
    }

    // V8 (ClearScript) runs its background tasks on .NET thread pool threads and frees pooled heap pages asynchronously
    // after an engine is disposed. Regular exit() runs C++ static destructors of ClearScriptV8 while such a task can
    // still be running, which crashes the process with SIGSEGV (exit code 139) after successful build. Registered last,
    // so all other ProcessExit handlers already ran, it ends the process without running native destructors.
    static void SkipNativeDestructorsOnExit()
    {
        if (OperatingSystem.IsWindows())
            return;
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            Console.Out.Flush();
            Console.Error.Flush();
            SysExit(Environment.ExitCode);
        };
    }

    [DllImport("libc", EntryPoint = "_exit")]
    static extern void SysExit(int status);
}
