using Lib.Utils.CommandLineParser.Parser;

namespace Lib.Utils.CommandLineParser.Definitions
{
    public class JsGlobalsCommand : CommandLineCommand
    {
        public override string[] Words => new[] { "global", "globals" };

        protected override string Description => "Prints global variables from JS";

        public CommandLineArgumentString FileName { get; } = new CommandLineArgumentString("filename.js", null);

        public CommandLineArgumentSwitch IncludeWellKnown { get; } = new CommandLineArgumentSwitch(
            "include well known browser globals",
            new[] {"-I", "--includeWellKnown"});

    }
}
