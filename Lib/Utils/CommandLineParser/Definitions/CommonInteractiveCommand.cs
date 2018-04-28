using Lib.Utils.CommandLineParser.Parser;

namespace Lib.Utils.CommandLineParser.Definitions
{
    public abstract class CommonInteractiveCommand : CommonParametersBaseCommand
    {
        public CommandLineArgumentString Port { get; } = new CommandLineArgumentString(description: "set port for server to listen to", words: new[] { "-p", "--port" }, defaultValue: "8080");

        public CommandLineArgumentBool Sprite { get; } = new CommandLineArgumentBool(description: "enable/disable creation of sprites", words: new[] { "--sprite" }, defaultValue: false);

        public CommandLineArgumentString VersionDir { get; } = new CommandLineArgumentString(description: "store all resources except index.html in this directory", words: new[] { "-v", "--versiondir" });

        public CommandLineArgumentSwitch BindToAny { get; } = new CommandLineArgumentSwitch(description: "allow to receive connections from external computers", words: new[] { "--bindToAny" });
    }
}
