namespace Lib.Utils.CommandLineParser.Definitions
{
    public class BuildInteractiveNoUpdateCommand : CommonInteractiveCommand
    {
        public override string[] Words => new[] { "y", "interactiveNoUpdate" };

        protected override string Description => "runs web controlled build ui without updating dependencies";
    }
}
