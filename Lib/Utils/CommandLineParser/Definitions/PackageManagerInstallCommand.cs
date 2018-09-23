using Lib.Utils.CommandLineParser.Parser;

namespace Lib.Utils.CommandLineParser.Definitions
{
    public class PackageManagerInstallCommand : CommandLineCommand
    {
        public override string[] Words { get; } = {"i", "install"};

        protected override string Description { get; } = "install packages by rules from package.json";
    }
}