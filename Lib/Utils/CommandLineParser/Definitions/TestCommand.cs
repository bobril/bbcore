using Lib.Utils.CommandLineParser.Parser;

namespace Lib.Utils.CommandLineParser.Definitions
{
    /// <summary>
    /// Build command with tests
    /// </summary>
    public class TestCommand : CommandLineCommand
    {
        /// <summary>
        /// Command aliases
        /// </summary>
        public override string[] Words => new[] { "test" };

        /// <summary>
        /// Command description
        /// </summary>
        protected override string Description => "runs tests once in Chrome";

        /// <summary>
        /// Out
        /// </summary>
        public CommandLineArgumentString Out { get; private set; } = new CommandLineArgumentString(description: "filename for test result as JUnit XML", words: new[] { "-o", "--out" });
    }
}
