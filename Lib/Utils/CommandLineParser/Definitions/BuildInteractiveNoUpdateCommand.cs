using Lib.Utils.CommandLineParser.Parser;

namespace Lib.Utils.CommandLineParser.Definitions
{
    /// <summary>
    /// Build command interactive no updates
    /// </summary>
    public class BuildInteractiveNoUpdateCommand : CommandLineCommand
    {
        /// <summary>
        /// Command aliases
        /// </summary>
        public override string[] Words => new[] { "y", "interactiveNoUpdate" };

        /// <summary>
        /// Command description
        /// </summary>
        protected override string Description => "runs web controlled build ui without updating dependencies";

        /// <summary>
        /// Port
        /// </summary>
        public CommandLineArgumentString Port { get; private set; } = new CommandLineArgumentString(description: "set port for server to listen to", words: new[] { "-p", "--port" }, defaultValue: "8080");
    }
}
