using Lib.Utils.CommandLineParser.Parser;

namespace Lib.Utils.CommandLineParser.Definitions
{
    public abstract class CommonParametersBaseCommand : CommandLineCommand
    {
        public CommandLineArgumentSwitch Verbose { get; } = new CommandLineArgumentSwitch(description: "enable spamming console output", words: new[] { "--verbose" });

        public CommandLineArgumentSwitch NoBuildCache { get; } = new CommandLineArgumentSwitch(description: "forbid using Build Cache", words: new[] { "--nocache" });
    }
}
