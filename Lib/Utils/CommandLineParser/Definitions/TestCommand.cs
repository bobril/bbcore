using Lib.Utils.CommandLineParser.Parser;

namespace Lib.Utils.CommandLineParser.Definitions
{
    public class TestCommand : CommandLineCommand
    {
        public override string[] Words => new[] { "test" };

        protected override string Description => "runs tests once in Chrome";

        public CommandLineArgumentString Out { get; } = new CommandLineArgumentString(description: "filename for test result as JUnit XML", words: new[] { "-o", "--out" });

        public CommandLineArgumentBool Sprite { get; } = new CommandLineArgumentBool(description: "enable/disable creation of sprites", words: new[] { "--sprite" }, defaultValue: false);

        public CommandLineArgumentSwitch Verbose { get; } = new CommandLineArgumentSwitch(description: "enable spamming console output", words: new[] { "--verbose" });
    }
}
