using System;
using Lib.DiskCache;
using Lib.Utils.Logger;

namespace bb;

class Program
{
    static void Main(string[] args)
    {
        var composition = new Lib.Composition.Composition(
            inDocker: Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER")!=null,
            new ConsoleLogger(),
            new NativeFsAbstraction());
        
        composition.ParseCommandLine(args);
        composition.RunCommand();
    }
}