using Lib.Utils.CommandLineParser.Parser;

namespace Lib.Utils.CommandLineParser.Definitions;

public class TestCommand : CommonParametersBaseCommand
{
    public override string[] Words => new[] { "test" };

    protected override string Description => "runs tests once in Chrome";

    public CommandLineArgumentString Out { get; } = new("filename for test result as JUnit XML", new[] { "-o", "--out" });

    public CommandLineArgumentBool FlatTestSuites { get; } = new(
        "use flat structure of test suites (to increase viewer compatibility)",
        new[] {"--flat"},
        true
    );

    public CommandLineArgumentSwitch PrintFailed { get; } = new(
        "print names of all failed tests",
        new[] {"-w", "--printfailed"}
    );

    public CommandLineArgumentBool Sprite { get; } = new("enable/disable creation of sprites", new[] { "--sprite" });

    public CommandLineArgumentString Port { get; } = new("set port for test server to listen to (default: first free)", new[] { "-p", "--port" });

    public CommandLineArgumentBoolNullable Localize { get; } = new("create localized resources (default: autodetect)", new[] { "-l", "--localize" });

    public CommandLineArgumentString Dir { get; } = new("where to just write test bundle", words: new[] { "-d", "--dir" });

    public CommandLineArgumentString SpecFilter { get; } = new("enable/disable tests matching a pattern", words: new[] { "-f", "--filter" });

    public CommandLineArgumentEnumValues Coverage { get; } = new(
        "calculate code coverage",
        new[] {"-c", "--coverage"},
        new[] { "json-details", "json-summary", "spa", "sonar", "none" }
    );
}