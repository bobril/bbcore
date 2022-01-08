using System.Collections.Generic;
using Lib.Utils.CommandLineParser.Parser;

namespace Lib.Utils.CommandLineParser.Definitions;

public class PackageManagerCommand : CommandLineCommand
{
    public override string[] Words { get; } = {"p", "package"};

    protected override string Description { get; } = "package management commands";

    public override List<CommandLineCommand> SubCommands { get; } = new List<CommandLineCommand>
    {
        new PackageManagerUpgradeCommand(),
        new PackageManagerInstallCommand(),
        new PackageManagerAddCommand()
    };
}