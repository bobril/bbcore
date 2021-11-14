using Lib.Utils.CommandLineParser.Parser;

namespace Lib.Utils.CommandLineParser.Definitions;

public class VisualizeSourceMapCommand : CommandLineCommand
{
    public override string[] Words => new[] { "sourcemap" };

    protected override string Description => "Writes html visualization of source map from js file";

    public CommandLineArgumentString FileName { get; } = new("filename.js", null);
}