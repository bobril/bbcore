using Lib.Utils.CommandLineParser.Parser;

namespace Lib.Utils.CommandLineParser.Definitions
{
    public class TestCommand : CommonParametersBaseCommand
    {
        public override string[] Words => new[] { "test" };

        protected override string Description => "runs tests once in Chrome";

        public CommandLineArgumentString Out { get; } = new CommandLineArgumentString("filename for test result as JUnit XML", new[] { "-o", "--out" });

        public CommandLineArgumentBool FlatTestSuites { get; } = new CommandLineArgumentBool(
            "use flat structure of test suites (to increase viewer compatibility)",
            new[] {"--flat"},
            true
        );

        public CommandLineArgumentBool Sprite { get; } = new CommandLineArgumentBool("enable/disable creation of sprites", new[] { "--sprite" });

        public CommandLineArgumentString Port { get; } = new CommandLineArgumentString("set port for test server to listen to (default: first free)", new[] { "-p", "--port" });

        public CommandLineArgumentBoolNullable Localize { get; } = new CommandLineArgumentBoolNullable("create localized resources (default: autodetect)", new[] { "-l", "--localize" });

        public CommandLineArgumentString Dir { get; } = new CommandLineArgumentString("where to just write test bundle", words: new[] { "-d", "--dir" });

        public CommandLineArgumentString SpecFilter { get; } = new CommandLineArgumentString("enable/disable tests matching a pattern", words: new[] { "-f", "--filter" });
    }
}
