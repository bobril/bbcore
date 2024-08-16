using Lib.Utils.CommandLineParser.Parser;

namespace Lib.Utils.CommandLineParser.Definitions;

public class JsLocateCommand : CommandLineCommand
{
    public override string[] Words => ["locate"];

    protected override string Description =>
        "Based on location like https://example.com/a.js:187:15789 it will beautify and locate this position in file";

    public CommandLineArgumentString FileNameWithPos { get; } = new("https://example.com/a.js:line:column", null);
}