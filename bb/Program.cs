using System;

namespace bb;

class Program
{
    static void Main(string[] args)
    {
        var composition = new Lib.Composition.Composition(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER")!=null);
        composition.ParseCommandLine(args);
        composition.RunCommand();
    }
}