using Lib.Utils.CommandLineParser.Parser;

namespace Lib.Utils.CommandLineParser.Definitions
{
    public class BuildInteractiveCommand : CommandLineCommand
    {
        public override string[] Words => new[] { "", "i", "interactive" };

        protected override string Description => "runs web controlled build ui";

        public CommandLineArgumentString Port { get; } = new CommandLineArgumentString(description: "set port for server to listen to", words: new[] { "-p", "--port" }, defaultValue: "8080");

        public CommandLineArgumentSwitch Verbose { get; } = new CommandLineArgumentSwitch(description: "enable spamming console output", words: new[] { "--verbose" });
    }
}
