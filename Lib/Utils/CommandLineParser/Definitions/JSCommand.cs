using System.Collections.Generic;
using Lib.Utils.CommandLineParser.Parser;

namespace Lib.Utils.CommandLineParser.Definitions
{
    public class JsCommand : CommandLineCommand
    {
        public override string[] Words => new[] { "js" };

        protected override string Description => "various commands to work with JavaScript";

        public override List<CommandLineCommand> SubCommands { get; } = new List<CommandLineCommand>
        {
            new JsGlobalsCommand()
        };
    }
}
