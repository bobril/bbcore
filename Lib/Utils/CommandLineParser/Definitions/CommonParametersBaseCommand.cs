using Lib.Utils.CommandLineParser.Parser;

namespace Lib.Utils.CommandLineParser.Definitions;

public abstract class CommonParametersBaseCommand : CommandLineCommand
{
    public CommandLineArgumentSwitch Verbose { get; } = new CommandLineArgumentSwitch(
        "enable spamming console output",
        new[] {"--verbose"});

    public CommandLineArgumentSwitch NoBuildCache { get; } =
        new CommandLineArgumentSwitch("forbid using Build Cache", new[] {"--nocache"});

    public CommandLineArgumentString VersionDir { get; } =
        new CommandLineArgumentString("store all resources except index.html in this directory",
            new[] {"-v", "--versiondir"});

    public CommandLineArgumentString SpriteVersionDir { get; } =
        new CommandLineArgumentString("override path to sprite bundle in resulting code",
            new[] {"--spriteversiondir"});
}