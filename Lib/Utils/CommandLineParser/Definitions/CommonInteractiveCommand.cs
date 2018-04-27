using Lib.Utils.CommandLineParser.Parser;

namespace Lib.Utils.CommandLineParser.Definitions
{
    public abstract class CommonInteractiveCommand : CommonParametersBaseCommand
    {
        public CommandLineArgumentString VersionDir { get; } = new CommandLineArgumentString(description: "store all resources except index.html in this directory", words: new[] { "-v", "--versiondir" });
    }
}
