using Lib.Utils.CommandLineParser.Parser;

namespace Lib.Utils.CommandLineParser.Definitions
{
    public class PackageManagerAddCommand : CommandLineCommand
    {
        public override string[] Words { get; } = {"a", "add"};

        protected override string Description { get; } = "add package";

        public CommandLineArgumentString PackageName { get; } = new CommandLineArgumentString("package name", null);

        public CommandLineArgumentSwitch Dev { get; } = new CommandLineArgumentSwitch(
            "add it as devDependency",
            new[] {"-D", "--dev"});
    }
}