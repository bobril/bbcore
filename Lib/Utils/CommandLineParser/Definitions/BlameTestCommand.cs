using Lib.Utils.CommandLineParser.Parser;

namespace Lib.Utils.CommandLineParser.Definitions
{
    public class BlameTestCommand : CommonParametersBaseCommand
    {
        public override string[] Words => new[] { "blametest" };

        protected override string Description => "runs many tests twice to find bug in Headless Chrome";

        public CommandLineArgumentBool Sprite { get; } = new CommandLineArgumentBool("enable/disable creation of sprites", new[] { "--sprite" }, false);

        public CommandLineArgumentString Port { get; } = new CommandLineArgumentString("set port for test server to listen to (default: first free)", new[] { "-p", "--port" });

        public CommandLineArgumentBoolNullable Localize { get; } = new CommandLineArgumentBoolNullable("create localized resources (default: autodetect)", new[] { "-l", "--localize" });
    }
}