namespace Lib.Utils.CommandLineParser.Definitions;

public class GenerateTsConfigCommand : CommonInteractiveCommand
{
    public override string[] Words => new[] { "gen-tsconfig" };

    protected override string Description => "generates tsconfig.json";
}