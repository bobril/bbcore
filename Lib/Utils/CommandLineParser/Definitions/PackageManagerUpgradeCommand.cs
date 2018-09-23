using Lib.Utils.CommandLineParser.Parser;

namespace Lib.Utils.CommandLineParser.Definitions
{
    public class PackageManagerUpgradeCommand : CommandLineCommand
    {
        public override string[] Words { get; } = {"u", "up", "upgrade"};

        protected override string Description { get; } = "upgrade all or specific package by rules from package.json";

        public CommandLineArgumentString PackageName { get; } = new CommandLineArgumentString("package name", null);
    }
}