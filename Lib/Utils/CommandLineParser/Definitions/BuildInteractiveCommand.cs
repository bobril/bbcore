using Lib.Utils.CommandLineParser.Parser;

namespace Lib.Utils.CommandLineParser.Definitions
{
    /// <summary>
    /// Build command interactive
    /// </summary>
    public class BuildInteractiveCommand : CommandLineCommand
    {
        /// <summary>
        /// Command aliases
        /// </summary>
        public override string[] Words => new[] { "", "i", "interactive" };

        /// <summary>
        /// Command description
        /// </summary>
        protected override string Description => "runs web controlled build ui";

        /// <summary>
        /// Port
        /// </summary>
        public CommandLineArgumentString Port { get; private set; } = new CommandLineArgumentString(description: "set port for server to listen to", words: new[] { "-p", "--port" }, defaultValue: "8080");
    }
}
