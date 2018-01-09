using Lib.Utils.CommandLineParser.Parser;

namespace Lib.Utils.CommandLineParser.Definitions
{
    public class BuildInteractiveNoUpdateCommand : CommandLineCommand
    {
        public override string[] Words => new[] { "y", "interactiveNoUpdate" };

        protected override string Description => "runs web controlled build ui without updating dependencies";

        public CommandLineArgumentString Port { get; } = new CommandLineArgumentString(description: "set port for server to listen to", words: new[] { "-p", "--port" }, defaultValue: "8080");

        public CommandLineArgumentSwitch Verbose { get; } = new CommandLineArgumentSwitch(description: "enable spamming console output", words: new[] { "--verbose" });
    }
}
