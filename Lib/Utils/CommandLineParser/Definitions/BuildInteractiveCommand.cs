using Lib.Utils.CommandLineParser.Parser;

namespace Lib.Utils.CommandLineParser.Definitions
{
    public class BuildInteractiveCommand : CommonInteractiveCommand
    {
        public override string[] Words => new[] { "", "i", "interactive" };

        protected override string Description => "runs web controlled build ui";
    }
}
