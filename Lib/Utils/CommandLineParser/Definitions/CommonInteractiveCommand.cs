using Lib.Utils.CommandLineParser.Parser;

namespace Lib.Utils.CommandLineParser.Definitions
{
    public abstract class CommonInteractiveCommand : CommonParametersBaseCommand
    {
        public CommandLineArgumentString Port { get; } =
            new CommandLineArgumentString("set port for server to listen to", new[] {"-p", "--port"}, "8080");

        public CommandLineArgumentBool Sprite { get; } =
            new CommandLineArgumentBool("enable/disable creation of sprites", new[] {"--sprite"}, false);

        public CommandLineArgumentString VersionDir { get; } =
            new CommandLineArgumentString("store all resources except index.html in this directory",
                new[] {"-v", "--versiondir"});

        public CommandLineArgumentSwitch BindToAny { get; } =
            new CommandLineArgumentSwitch("allow to receive connections from external computers",
                new[] {"--bindToAny"});

        public CommandLineArgumentBoolNullable Localize { get; } =
            new CommandLineArgumentBoolNullable("create localized resources (default: autodetect)",
                new[] {"-l", "--localize"});

        public CommandLineArgumentString ProxyBB { get; } =
            new CommandLineArgumentString("bobril-build dev only feature proxy bb to", new[] {"--proxybb"});

        public CommandLineArgumentString ProxyBBTest { get; } =
            new CommandLineArgumentString("bobril-build dev only feature proxy bb/test to", new[] {"--proxybbtest"});
    }
}
